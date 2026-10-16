using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class SalesTests(Fixture f)
{
    [Fact]public async Task ConfirmationCapturesHistoricalCostAndPreventsDuplicateDeduction()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await InventoryTests.Product(s,a,10);await s.Adjust(a,new AdjustmentInput(id,5,"Opening",2,Guid.NewGuid()));
        var doc=await s.Draft(f.Users[Roles.Sales],new DraftInput(DocumentKind.Sale,null,[new ItemInput(id,2,999)],Key:Guid.NewGuid()));var key=Guid.NewGuid();await s.Confirm(f.Users[Roles.Sales],doc,key);await s.Confirm(f.Users[Roles.Sales],doc,key);
        Assert.Equal(3,(await s.Product(a,id)).Stock);Assert.Equal(10,(await s.Document(a,doc)).Items[0].UnitPrice);Assert.Equal(2,(await s.Document(a,doc)).Items[0].UnitCost);Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.Confirm(a,doc,Guid.NewGuid()))).Status);
        await s.Adjust(a,new AdjustmentInput(id,3,"Later cost change",6,Guid.NewGuid()));Assert.Equal(2,(await s.Document(a,doc)).Items[0].UnitCost);
        Assert.Equal(404,(await Assert.ThrowsAsync<BusinessException>(()=>s.Document(f.Users["OtherSales"],doc))).Status);
    }
    [Fact]public async Task InsufficientStockAndStalePricesLeaveDraftAndLedgerUntouched()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await InventoryTests.Product(s,a);var doc=await s.Draft(a,new DraftInput(DocumentKind.Sale,null,[new ItemInput(id,1)],Key:Guid.NewGuid()));
        await Assert.ThrowsAsync<BusinessException>(()=>s.Confirm(a,doc,Guid.NewGuid()));Assert.Equal(0,(await s.Movements(a,id)).Total);Assert.Equal(DocumentState.Draft,(await s.Document(a,doc)).State);
        await s.Adjust(a,new AdjustmentInput(id,2,"Count",2,Guid.NewGuid()));var p=await s.Product(a,id);await s.SaveProduct(a,id,new ProductInput(p.Sku,p.Name,p.CategoryId,p.UnitId,20,2));
        await Assert.ThrowsAsync<BusinessException>(()=>s.Confirm(a,doc,Guid.NewGuid()));Assert.Equal(2,(await s.Product(a,id)).Stock);
    }
    [Fact]public async Task PaymentsRefundsAndFullReturnsAreIndependentAndIdempotent()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await InventoryTests.Product(s,a);await s.Adjust(a,new AdjustmentInput(id,5,"Opening",2,Guid.NewGuid()));var doc=await s.Draft(a,new DraftInput(DocumentKind.Sale,null,[new ItemInput(id,2)],Key:Guid.NewGuid()));await s.Confirm(a,doc,Guid.NewGuid());
        var pay=new PaymentInput(15,PaymentMethod.Cash,Guid.NewGuid());await s.RecordPayment(a,doc,pay);await s.RecordPayment(a,doc,pay);Assert.Equal("PartiallyPaid",(await s.Document(a,doc)).PaymentState);await Assert.ThrowsAsync<BusinessException>(()=>s.RecordPayment(a,doc,pay with{Amount=6,Key=Guid.NewGuid()}));
        var refund=new PaymentInput(5,PaymentMethod.Cash,Guid.NewGuid(),"Courtesy refund");await s.RecordPayment(a,doc,refund,true);await s.RecordPayment(a,doc,refund,true);Assert.Equal(3,(await s.Product(a,id)).Stock);await Assert.ThrowsAsync<BusinessException>(()=>s.RecordPayment(a,doc,refund with{Amount=11,Key=Guid.NewGuid()},true));
        var reverse=new ReverseInput("All goods returned",Guid.NewGuid());await s.Reverse(a,doc,reverse);await s.Reverse(a,doc,reverse);Assert.Equal(5,(await s.Product(a,id)).Stock);Assert.Equal("RefundDue",(await s.Document(a,doc)).PaymentState);await Assert.ThrowsAsync<BusinessException>(()=>s.Reverse(a,doc,reverse with{Key=Guid.NewGuid()}));
        await s.RecordPayment(a,doc,new PaymentInput(10,PaymentMethod.Cash,Guid.NewGuid(),"Final reversal refund"),true);var d=await s.Document(a,doc);Assert.Equal("Reversed",d.PaymentState);Assert.Equal(0,d.Outstanding);Assert.Equal(3,d.Payments.Count);
    }
}
