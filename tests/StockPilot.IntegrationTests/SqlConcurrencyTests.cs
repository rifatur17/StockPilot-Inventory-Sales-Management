using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKPILOT_TEST_SQLSERVER")))
            Skip = "Requires isolated SQL Server; SQLite does not prove SQL locking.";
    }
}
[Collection("database")]
public sealed class SqlConcurrencyTests(Fixture f)
{
    private async Task<(bool Ok, Guid Id)> Run(Func<BusinessService, Task<Guid>> command)
    {
        using var scope = f.Factory.Services.CreateScope();
        try
        {
            return (true, await command(scope.ServiceProvider.GetRequiredService<BusinessService>()));
        }
        catch (BusinessException e) when (e.Status == 409) { return (false, Guid.Empty); }
    }
    [SqlFact]
    public async Task TwoSalesCompetingForOneUnitCannotOversell()
    {
        using var scope = f.Factory.Services.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<BusinessService>();
        var a = f.Users[Roles.Admin];
        var p = await InventoryTests.Product(s, a);
        await s.Adjust(a, new AdjustmentInput(p, 1, "Concurrency stock", 2, Guid.NewGuid()));
        var one = await s.Draft(a, new DraftInput(DocumentKind.Sale, null, [new ItemInput(p, 1)], Key: Guid.NewGuid()));
        var two = await s.Draft(a, new DraftInput(DocumentKind.Sale, null, [new ItemInput(p, 1)], Key: Guid.NewGuid()));
        var results = await Task.WhenAll(Run(x => x.Confirm(a, one, Guid.NewGuid())), Run(x => x.Confirm(a, two, Guid.NewGuid())));
        Assert.Single(results, x => x.Ok);
        Assert.Equal(0, (await s.Product(a, p)).Stock);
        Assert.Equal(2, (await s.Movements(a, p)).Total);
    }
    [SqlFact]
    public async Task ConcurrentDuplicateReceiptAndPaymentCommitExactlyOnce()
    {
        using var scope = f.Factory.Services.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<BusinessService>();
        var a = f.Users[Roles.Admin];
        var p = await InventoryTests.Product(s, a);
        var supplier = await s.SavePartner(a, "Supplier", null, new PartnerInput("Concurrent supplier"));
        var purchase = await s.Draft(a, new DraftInput(DocumentKind.Purchase, supplier, [new ItemInput(p, 2, 2)], Key: Guid.NewGuid()));
        var key = Guid.NewGuid();
        Assert.All(await Task.WhenAll(Run(x => x.Receive(a, purchase, key)), Run(x => x.Receive(a, purchase, key))), x => Assert.True(x.Ok));
        Assert.Equal(2, (await s.Product(a, p)).Stock);
        Assert.Equal(1, (await s.Movements(a, p)).Total);
        var sale = await s.Draft(a, new DraftInput(DocumentKind.Sale, null, [new ItemInput(p, 1)], Key: Guid.NewGuid()));
        var confirm = Guid.NewGuid();
        Assert.All(await Task.WhenAll(Run(x => x.Confirm(a, sale, confirm)), Run(x => x.Confirm(a, sale, confirm))), x => Assert.True(x.Ok));
        var payment = new PaymentInput(10, PaymentMethod.Cash, Guid.NewGuid());
        var results = await Task.WhenAll(Run(x => x.RecordPayment(a, sale, payment)), Run(x => x.RecordPayment(a, sale, payment)));
        Assert.All(results, x => Assert.True(x.Ok));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single((await s.Document(a, sale)).Payments);
    }
    [SqlFact]
    public async Task DuplicateReturnAndRefundCannotRestoreStockOrMoneyTwice()
    {
        using var scope = f.Factory.Services.CreateScope();
        var s = scope.ServiceProvider.GetRequiredService<BusinessService>();
        var a = f.Users[Roles.Admin];
        var p = await InventoryTests.Product(s, a);
        await s.Adjust(a, new AdjustmentInput(p, 1, "Concurrency return", 2, Guid.NewGuid()));
        var doc = await s.Draft(a, new DraftInput(DocumentKind.Sale, null, [new ItemInput(p, 1)], Key: Guid.NewGuid()));
        await s.Confirm(a, doc, Guid.NewGuid());
        await s.RecordPayment(a, doc, new PaymentInput(10, PaymentMethod.Cash, Guid.NewGuid()));
        var input = new ReverseInput("Physical return", Guid.NewGuid());
        Assert.All(await Task.WhenAll(Run(x => x.Reverse(a, doc, input)), Run(x => x.Reverse(a, doc, input))), x => Assert.True(x.Ok));
        Assert.Equal(1, (await s.Product(a, p)).Stock);
        var first = new PaymentInput(10, PaymentMethod.Cash, Guid.NewGuid(), "First refund");
        var second = new PaymentInput(10, PaymentMethod.Cash, Guid.NewGuid(), "Competing refund");
        var results = await Task.WhenAll(Run(x => x.RecordPayment(a, doc, first, true)), Run(x => x.RecordPayment(a, doc, second, true)));
        Assert.Single(results, x => x.Ok);
        Assert.Equal(10, (await s.Document(a, doc)).Refunded);
    }
}
