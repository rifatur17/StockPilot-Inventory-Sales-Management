using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class PurchaseTests(Fixture f)
{
    [Fact]public async Task ReceiptIsAtomicIdempotentAndDiscountCosted()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await InventoryTests.Product(s,a);var supplier=await s.SavePartner(a,"Supplier",null,new PartnerInput("Receipt supplier"));
        var draft=new DraftInput(DocumentKind.Purchase,supplier,[new ItemInput(id,10,4)],4,10,Key:Guid.NewGuid());var doc=await s.Draft(a,draft);Assert.Equal(doc,await s.Draft(a,draft));Assert.Equal(0,(await s.Product(a,id)).Stock);
        var key=Guid.NewGuid();await s.Receive(a,doc,key);await s.Receive(a,doc,key);Assert.Equal(10,(await s.Product(a,id)).Stock);Assert.Equal(3.6m,(await s.Product(a,id)).AverageCost);Assert.Equal(1,(await s.Movements(a,id)).Total);
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.Receive(a,doc,Guid.NewGuid()))).Status);Assert.Equal(DocumentState.Completed,(await s.Document(a,doc)).State);
    }
    [Fact]public async Task InvalidReceiptRollsBackEarlierLinesAndCancellationNeverAddsStock()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var one=await InventoryTests.Product(s,a);var two=await InventoryTests.Product(s,a);var supplier=await s.SavePartner(a,"Supplier",null,new PartnerInput("Rollback supplier"));var doc=await s.Draft(a,new DraftInput(DocumentKind.Purchase,supplier,[new ItemInput(one,1,2),new ItemInput(two,1,2)],Key:Guid.NewGuid()));
        await s.ArchiveProduct(a,two);await Assert.ThrowsAsync<BusinessException>(()=>s.Receive(a,doc,Guid.NewGuid()));Assert.Equal(0,(await s.Product(a,one)).Stock);Assert.Equal(0,(await s.Movements(a,one)).Total);Assert.Equal(DocumentState.Draft,(await s.Document(a,doc)).State);
        await s.Cancel(a,doc,Guid.NewGuid());Assert.Equal(DocumentState.Cancelled,(await s.Document(a,doc)).State);await Assert.ThrowsAsync<BusinessException>(()=>s.Receive(a,doc,Guid.NewGuid()));
    }
}
