using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class InventoryTests(Fixture f)
{
    public static async Task<Guid> Product(BusinessService s,string actor,decimal price=10)
    {var key=Guid.NewGuid().ToString();var c=await s.SaveLookup(actor,"categories",null,"Category "+key);var u=await s.SaveLookup(actor,"units",null,"Unit "+key);return await s.SaveProduct(actor,null,new ProductInput("P-"+key,"Ledger product "+key,c,u,price,2));}
    [Fact]public async Task AdjustmentsAreIdempotentCostedAndAudited()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await Product(s,a);var input=new AdjustmentInput(id,10,"Opening count",2,Guid.NewGuid());
        await s.Adjust(a,input);await s.Adjust(a,input);Assert.Equal(10,(await s.Product(a,id)).Stock);
        await s.Adjust(a,new AdjustmentInput(id,10,"Further count",6,Guid.NewGuid()));Assert.Equal(4,(await s.Product(a,id)).AverageCost);
        var movements=await s.Movements(a,id);Assert.Equal(2,movements.Total);Assert.All(movements.Items,x=>Assert.Equal(x.After,x.Before+x.Change));
        Assert.Contains((await s.AuditHistory(a,size:100)).Items,x=>x.Action=="StockAdjusted" && x.EntityId==id.ToString());
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.Adjust(a,input with{Change=5}))).Status);
    }
    [Fact]public async Task InvalidAdjustmentRollsBackAllWrites()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var id=await Product(s,a);var key=Guid.NewGuid();
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.Adjust(a,new AdjustmentInput(id,-1,"Too much",0,key)))).Status);
        Assert.Equal(0,(await s.Product(a,id)).Stock);Assert.Equal(0,(await s.Movements(a,id)).Total);Assert.False(await scope.ServiceProvider.GetRequiredService<AppDb>().Operations.AnyAsync(x=>x.Key==key));
        await Assert.ThrowsAsync<BusinessException>(()=>s.Adjust(f.Users[Roles.Sales],new AdjustmentInput(id,1,"Forbidden",2,key)));
    }
}
