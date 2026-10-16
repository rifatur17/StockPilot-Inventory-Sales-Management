using Microsoft.Extensions.DependencyInjection;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;
[Collection("database")]
public sealed class SettingsTests(Fixture f)
{
    [Fact]public async Task CurrencyCannotChangeOnceAnyDocumentExists()
    {
        using var scope=f.Factory.Services.CreateScope();var s=scope.ServiceProvider.GetRequiredService<BusinessService>();var a=f.Users[Roles.Admin];var p=await InventoryTests.Product(s,a);await s.Draft(a,new DraftInput(DocumentKind.Sale,null,[new ItemInput(p,1)],Key:Guid.NewGuid()));
        Assert.Equal(409,(await Assert.ThrowsAsync<BusinessException>(()=>s.SaveSettings(a,new SettingsInput("Business","EUR")))).Status);await Assert.ThrowsAsync<BusinessException>(()=>s.SaveSettings(f.Users[Roles.Manager],new SettingsInput("Business","XTS")));Assert.Equal("XTS",(await s.Settings(a)).CurrencyCode);
    }
}
