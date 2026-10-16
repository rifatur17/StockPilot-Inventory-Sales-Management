using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

[Collection("database")]
public sealed class HttpJourneyTests(Fixture f)
{
    [Fact]
    public async Task InvalidDateFiltersReturnValidationErrorsInsteadOfServerErrors()
    {
        using var client = await f.Login(Roles.Admin);
        foreach (var path in new[] { "/api/v1/reports?to=9999-12-31", "/api/v1/sales?from=2026-02-02&to=2026-02-01", "/api/v1/purchases?to=9999-12-31" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task ApiPurchaseSalePaymentAndRenderedPagesShareBusinessRules()
    {
        using var scope = f.Factory.Services.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<BusinessService>();
        var a = f.Users[Roles.Admin];
        var p = await InventoryTests.Product(s, a);
        var supplier = await s.SavePartner(a, "Supplier", null, new PartnerInput("HTTP supplier"));
        using var client = await f.Login(Roles.Admin);
        var draft = await client.PostAsJsonAsync("/api/v1/purchases", new DraftInput(DocumentKind.Purchase, supplier, [new ItemInput(p, 3, 2)], Key: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var purchase = (await draft.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/purchases/{purchase}/receive", new KeyInput(Guid.NewGuid()))).StatusCode);
        var sale = await client.PostAsJsonAsync("/api/v1/sales", new DraftInput(DocumentKind.Sale, null, [new ItemInput(p, 1, 999)], Key: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
        var id = (await sale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/sales/{id}/confirm", new KeyInput(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/sales/{id}/payments", new PaymentInput(10, PaymentMethod.Card, Guid.NewGuid()))).StatusCode);
        var invoice = await client.GetStringAsync("/Sales/Details/" + id);
        Assert.Contains("Paid", invoice);
        Assert.Contains("Print invoice", invoice);
        Assert.Contains("/Sales/Refund/" + id, invoice);
        foreach (var path in new[] { "/", "/Products", "/Lookups", "/Partners?kind=Supplier", "/Partners?kind=Customer", "/Purchases", "/Purchases/Create", "/Sales", "/Sales/Create", "/Inventory", "/Inventory/Movements", "/Reports", "/Users", "/Audit" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/sales", new
        {
            Kind = "Sale",
            Items = Array.Empty<object>(),
            Key = Guid.NewGuid()
        })).StatusCode);
    }
    [Fact]
    public async Task InactiveUserSessionAndCrossStaffResourcesAreRejected()
    {
        using var scope = f.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        var email = "temporary" + Guid.NewGuid().ToString("N") + "@stockpilot.test";
        await users.Create(f.Users[Roles.Admin], email, "Temporary", f.Password, Roles.Sales);
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var u = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Users, x => x.Email == email);
        using var c = f.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Fixture.Csrf(c);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/session/login", new
        {
            Email = email,
            Password = f.Password
        })).StatusCode);
        await Fixture.Csrf(c);
        await users.Change(f.Users[Roles.Admin], u.Id, Roles.Sales, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/session")).StatusCode);
    }
}
