using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace StockPilot.Infrastructure;

public sealed class DesignFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args)
    {
        var cs = args.Contains("--design-only") ? "Server=design-only.invalid;Database=StockPilotDesign;Integrated Security=true;Encrypt=true" : Environment.GetEnvironmentVariable("ConnectionStrings__StockPilot");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Provide ConnectionStrings__StockPilot explicitly, or --design-only for offline migration generation.");
        return new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(cs).Options);
    }
}
