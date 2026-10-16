using Microsoft.EntityFrameworkCore;
using StockPilot.Infrastructure;
namespace StockPilot.IntegrationTests;

public sealed class SqlModelTests
{
    [Fact]
    public void StringForeignKeysHaveSameLengthAsTheirPrincipalKeys()
    {
        using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer("Server=design-only.invalid;Database=StockPilotDesign;Integrated Security=true").Options);
        foreach (var fk in db.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()))
        {
            for (var i = 0; i < fk.Properties.Count; i++)
                if (fk.Properties[i].ClrType == typeof(string))
                    Assert.Equal(fk.PrincipalKey.Properties[i].GetMaxLength(), fk.Properties[i].GetMaxLength());
        }
    }
}
