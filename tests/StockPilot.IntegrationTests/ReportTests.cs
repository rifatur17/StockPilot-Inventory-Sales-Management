using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class ReportTests(Fixture f)
{
    [Fact]public async Task ReportsUseHistoricCostsExcludeTaxAndRespectMonetaryCredits()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var before=await s.Report(a);var id=await InventoryTests.Product(s,a,10);await s.Adjust(a,new AdjustmentInput(id,5,"Report opening",2,Guid.NewGuid()));
        var doc=await s.Draft(a,new DraftInput(DocumentKind.Sale,null,[new ItemInput(id,2)],2,10,Key:Guid.NewGuid()));await s.Confirm(a,doc,Guid.NewGuid());await s.RecordPayment(a,doc,new PaymentInput(19.8m,PaymentMethod.Card,Guid.NewGuid()));await s.RecordPayment(a,doc,new PaymentInput(2.2m,PaymentMethod.Card,Guid.NewGuid(),"Credit"),true);
        var after=await s.Report(a);Assert.Equal(18,after.Revenue-before.Revenue);Assert.Equal(16,after.NetRevenue-before.NetRevenue);Assert.Equal(4,after.Cogs-before.Cogs);Assert.Equal(12,after.GrossProfitEstimate-before.GrossProfitEstimate);
        await s.Reverse(a,doc,new ReverseInput("Full return",Guid.NewGuid()));var reversed=await s.Report(a);Assert.Equal(before.NetRevenue,reversed.NetRevenue);Assert.Equal(before.Cogs,reversed.Cogs);
        await Assert.ThrowsAsync<BusinessException>(()=>s.Report(f.Users[Roles.Sales]));await Assert.ThrowsAsync<BusinessException>(()=>s.Report(a,DateTime.UtcNow,DateTime.UtcNow.AddDays(-1)));
    }
}
