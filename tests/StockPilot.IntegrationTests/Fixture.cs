using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

[CollectionDefinition("database", DisableParallelization = true)] public sealed class DatabaseCollection : ICollectionFixture<Fixture>;
public sealed class Fixture : IAsyncLifetime
{
    public string Connection { get; private set; } = "";
    public bool Sql => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKPILOT_TEST_SQLSERVER"));
    public string DatabaseName { get; } = "stockpilot_" + Guid.NewGuid().ToString("N") + "_test";
    public string Password { get; } = "Test!" + Guid.NewGuid().ToString("N");
    public Dictionary<string, string> Users { get; } = [];
    public AppFactory Factory { get; private set; } = null!;
    private string? file;
    public async Task InitializeAsync()
    {
        if (Sql)
        {
            var b = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STOCKPILOT_TEST_SQLSERVER"));
            if (b.InitialCatalog != "master")
                throw new InvalidOperationException("Test SQL connection must explicitly target master; a new uniquely named _test database is created. Never supply production credentials.");
            await using var con = new SqlConnection(b.ConnectionString);
            await con.OpenAsync();
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "IF DB_ID(@name) IS NOT NULL THROW 51001, 'Test database already exists', 1;";
            cmd.Parameters.AddWithValue("@name", DatabaseName);
            await cmd.ExecuteNonQueryAsync();
            cmd.Parameters.Clear();
            cmd.CommandText = "CREATE DATABASE [" + DatabaseName + "]";
            await cmd.ExecuteNonQueryAsync();
            b.InitialCatalog = DatabaseName;
            Connection = b.ConnectionString;
        }
        else
        {
            file = Path.Combine(Path.GetTempPath(), DatabaseName + ".db");
            if (File.Exists(file))
                throw new InvalidOperationException("Refusing existing file");
            Connection = "Data Source=" + file;
        }
        Factory = new AppFactory(this);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (Sql)
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
        db.Settings.Add(new BusinessSetting { Id = BusinessSetting.SingletonId, Name = "StockPilot test business", CurrencyCode = "XTS" });
        await db.SaveChangesAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in Roles.All)
        {
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            var u = new ApplicationUser { UserName = role + "@stockpilot.test", Email = role + "@stockpilot.test", DisplayName = role };
            Assert.True((await users.CreateAsync(u, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(u, role)).Succeeded);
            Users[role] = u.Id;
        }
        var extra = new ApplicationUser { UserName = "other@stockpilot.test", Email = "other@stockpilot.test", DisplayName = "Other sales" };
        Assert.True((await users.CreateAsync(extra, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(extra, Roles.Sales)).Succeeded);
        Users["OtherSales"] = extra.Id;
    }
    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    } // SQL database is never dropped by test code; disposable CI container is removed by workflow.
    public async Task<HttpClient> Login(string role)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Csrf(client);
        var email = role == "OtherSales" ? "other@stockpilot.test" : role + "@stockpilot.test";
        var res = await client.PostAsJsonAsync("/api/v1/session/login", new
        {
            Email = email,
            Password
        });
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        await Csrf(client);
        return client;
    }
    public static async Task Csrf(HttpClient c)
    {
        var json = await c.GetFromJsonAsync<JsonElement>("/api/v1/session/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("token").GetString());
    }
}
public sealed class AppFactory(Fixture fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("AllowedHosts", "localhost;127.0.0.1;[::1]");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<DbContextOptions<AppDb>>();
            s.RemoveAll<IDbContextOptionsConfiguration<AppDb>>();
            s.AddDbContext<AppDb>(o => { if (fixture.Sql) o.UseSqlServer(fixture.Connection); else o.UseSqlite(fixture.Connection); });
        });
    }
}
