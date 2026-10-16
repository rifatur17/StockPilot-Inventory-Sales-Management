using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
// All application writes, including authorization changes, share this database-scoped gate.
public sealed class LedgerGate(AppDb db, IHostEnvironment environment)
{
    private static readonly SemaphoreSlim TestGate = new(1);
    public async Task<T> Run<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        var testing = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" && environment.IsEnvironment("Testing");
        if (!db.Database.IsSqlServer() && !testing)
            throw new InvalidOperationException("Only SQL Server is supported outside isolated tests.");
        if (testing)
            await TestGate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!testing)
                await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'StockPilot.Ledger.v1', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Ledger is busy; retry safely with the same operation key.', 1;", ct);
            // Never reuse pre-lock tracked balances or user roles.
            db.ChangeTracker.Clear();
            var result = await action();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new BusinessException("Record changed concurrently. Refresh and retry with the same operation key.", 409); }
        catch (Exception ex) when (ex is SqlException || ex is DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if ((ex as SqlException ?? ex.InnerException as SqlException)?.Number is 1205 or 51000 or 2601 or 2627)
                throw new BusinessException("Conflicting or busy operation. Refresh; retry financial operations using the original key.", 409);
            throw;
        }
        finally { if (testing) TestGate.Release(); }
    }
}
