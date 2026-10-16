using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed partial class BusinessService(AppDb db, LedgerGate gate, UserService users)
{
    public Task<Actor> Actor(string id, CancellationToken ct = default) => users.Actor(id, ct);
    private static string Text(string? value, string field, int max, bool required = false)
    {
        var s = value?.Trim() ?? "";
        if ((required && s.Length == 0) || s.Length > max) throw new BusinessException($"{field} must {(required ? "contain 1–" : "not exceed ")}{max} characters.");
        return s;
    }
    private static (int Page, int Size) Paging(int page, int size) => (Math.Clamp(page, 1, 10000), Math.Clamp(size, 1, 100));
    private void Audit(string actor, string action, string type, Guid id, string summary = "") => db.Activities.Add(new ActivityLog { ActorId = actor, Action = action, EntityType = type, EntityId = id.ToString(), Summary = summary.Length == 0 ? action : summary });
    private async Task<Guid> Once(Actor actor, Guid key, string action, object payload, Func<Task<Guid>> work, CancellationToken ct)
    {
        if (key == Guid.Empty) throw new BusinessException("A nonempty UUID operation key is required. Reuse the same key and payload when retrying.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(action + JsonSerializer.Serialize(payload))));
        var prior = await db.Operations.SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, ct);
        if (prior is not null) { if (prior.Fingerprint != fingerprint) throw new BusinessException("Operation key already used with a different request.", 409); return prior.ResultId; }
        var result = await work(); db.Operations.Add(new Operation { ActorId = actor.Id, Key = key, Fingerprint = fingerprint, ResultId = result }); return result;
    }
}
