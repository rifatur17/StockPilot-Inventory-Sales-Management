using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

[Collection("database")]
public sealed class AuthTests(Fixture f)
{
    [Theory]
    [InlineData("/assets/site.css", "text/css")]
    [InlineData("/assets/bootstrap.min.css", "text/css")]
    [InlineData("/assets/app.js", "text/javascript")]
    [InlineData("/assets/cart.js", "text/javascript")]
    public async Task PublicStaticAssetsHaveCorrectContentTypes(string path, string contentType)
    {
        using var c = f.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        var response = await c.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }
    [Fact]
    public async Task AnonymousApiGets401AndMvcRedirects()
    {
        using var c = f.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/Account/Profile")).StatusCode);
        var login = await c.GetStringAsync("/Account/Login");
        Assert.Contains("__RequestVerificationToken", login);
        Assert.Contains("/assets/site.css", login);
    }
    [Fact]
    public async Task CsrfRequiredAndSalesCannotManageUsers()
    {
        using var c = await f.Login(Roles.Sales);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/users")).StatusCode);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/v1/session/logout", null)).StatusCode);
    }
    [Fact]
    public async Task ProfileRenderingUsesSavedValueAndValidatesEmptyInput()
    {
        using var c = await f.Login(Roles.Inventory);
        var response = await c.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string, string> { { "DisplayName", "Inventory specialist" } }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("value=\"Inventory specialist\"", await c.GetStringAsync("/Account/Profile"));
        var invalid = await c.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string, string> { { "DisplayName", "" } }));
        Assert.Contains("required", await invalid.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task LastActiveAdminCannotBeDemoted()
    {
        using var scope = f.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<UserService>();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.Change(f.Users[Roles.Admin], f.Users[Roles.Admin], Roles.Sales, false));
        Assert.Equal(409, ex.Status);
    }
    [Fact]
    public async Task LockoutUsesGenericFailure()
    {
        using var scope = f.Factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = new ApplicationUser { UserName = "locked@stockpilot.test", Email = "locked@stockpilot.test" };
        Assert.True((await manager.CreateAsync(u, f.Password)).Succeeded);
        using var c = f.Factory.CreateClient();
        await Fixture.Csrf(c);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/session/login", new
            {
                Email = u.Email,
                Password = "Incorrect!12345"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/session/login", new
        {
            Email = u.Email,
            Password = f.Password
        })).StatusCode);
        await scope.ServiceProvider.GetRequiredService<AppDb>().Entry(u).ReloadAsync();
        Assert.True(await manager.IsLockedOutAsync(u));
    }
}
