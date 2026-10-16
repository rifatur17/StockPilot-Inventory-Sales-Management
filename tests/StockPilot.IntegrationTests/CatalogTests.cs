using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
using System.Net;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class CatalogTests(Fixture f)
{
    [Fact] public async Task CatalogValidatesSkuAndProtectsReferencedLookups()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];
        var c=await s.SaveLookup(a,"categories",null,"Category "+Guid.NewGuid());var u=await s.SaveLookup(a,"units",null,"Unit "+Guid.NewGuid());
        var input=new ProductInput("sku-"+Guid.NewGuid(),"Widget",c,u,10,3);var id=await s.SaveProduct(a,null,input);
        var p=await s.Product(a,id);Assert.Equal(input.Sku.ToUpperInvariant(),p.Sku);Assert.Equal(0,p.Stock);
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.SaveProduct(a,null,input))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.DeleteLookup(a,"categories",c))).Status);
        Assert.Null((await s.Product(f.Users[Roles.Sales],id)).AverageCost);
        await Assert.ThrowsAsync<BusinessException>(()=>s.SaveProduct(f.Users[Roles.Sales],id,input));
        await s.ArchiveProduct(a,id);Assert.False((await s.Product(a,id)).Active);
    }
    [Fact] public async Task PartnersValidateKindAndRestrictSensitiveSupplierData()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];
        var id=await s.SavePartner(a,"Supplier",null,new PartnerInput("Supplier",Notes:"Private"));
        Assert.Equal("Private",(await s.Partner(a,id)).Notes);
        Assert.Equal(403,(await Assert.ThrowsAsync<BusinessException>(()=>s.Partner(f.Users[Roles.Sales],id))).Status);
        await s.ArchivePartner(a,id);Assert.False((await s.Partner(a,id)).Active);
    }
    [Fact] public async Task RenderedProductEditAndValidationPreserveValues()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];
        var c=await s.SaveLookup(a,"categories",null,"Forms "+Guid.NewGuid());var u=await s.SaveLookup(a,"units",null,"Forms "+Guid.NewGuid());
        var sku="FORM-"+Guid.NewGuid();var id=await s.SaveProduct(a,null,new ProductInput(sku,"Edit me",c,u,10,2));
        using var client=await f.Login(Roles.Admin);var html=await client.GetStringAsync("/Products/Edit/"+id);Assert.Contains("/Products/Edit/"+id,html);Assert.Contains("Edit me",html);Assert.Contains(sku.ToUpperInvariant(),html);
        var response=await client.PostAsync("/Products/Edit/"+id,new FormUrlEncodedContent(new Dictionary<string,string>{{"Sku",sku},{"Name","Keep submitted"},{"CategoryId",c.ToString()},{"UnitId",u.ToString()},{"SellingPrice","-1"}}));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);var invalid=await response.Content.ReadAsStringAsync();Assert.Contains("Keep submitted",invalid);Assert.Contains("nonnegative",invalid);
        var ok=await client.PostAsync("/Products/Edit/"+id,new FormUrlEncodedContent(new Dictionary<string,string>{{"Sku",sku},{"Name","Saved name"},{"CategoryId",c.ToString()},{"UnitId",u.ToString()},{"SellingPrice","15"}}));Assert.Equal(HttpStatusCode.Redirect,ok.StatusCode);Assert.Equal("Saved name",(await s.Product(a,id)).Name);
    }
}
