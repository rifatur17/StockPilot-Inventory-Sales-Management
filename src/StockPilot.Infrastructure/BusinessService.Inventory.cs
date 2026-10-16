using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;

public sealed partial class BusinessService
{
    private void Move(Product p, decimal change, string actor, string type, string reason, Guid? document = null)
    {
        var after = p.Stock + change;
        if (after < 0)
            throw new BusinessException("Insufficient stock. No changes were committed.", 409);
        if (after > 999999999)
            throw new BusinessException("Stock balance exceeds supported maximum.");
        db.Movements.Add(new StockMovement { ProductId = p.Id, DocumentId = document, Before = p.Stock, Change = change, After = after, ActorId = actor, Type = type, Reason = reason });
        p.Stock = after;
    }
    public Task<Guid> Adjust(string actorId, AdjustmentInput input, CancellationToken ct = default) => gate.Run(async () =>
    {
        var actor = await Actor(actorId, ct);
        actor.Demand(Roles.Manager, Roles.Inventory);
        return await Once(actor, input.Key, "adjust", input, async () =>
        {
            Money.Quantity(Math.Abs(input.Change));
            Money.Amount(input.UnitCost, "Unit cost");
            var reason = Text(input.Reason, "Reason", 300, true);
            var p = await db.Products.SingleOrDefaultAsync(x => x.Id == input.ProductId && x.Active, ct) ?? throw new BusinessException("Active product not found.", 404);
            if (input.Change > 0)
                p.AverageCost = Money.Average(p.Stock, p.AverageCost, input.Change, input.UnitCost);
            Move(p, input.Change, actorId, input.Change > 0 ? "AdjustmentIncrease" : "AdjustmentDecrease", reason);
            Audit(actorId, "StockAdjusted", "Product", p.Id, reason);
            return p.Id;
        }, ct);
    }, ct);
    public async Task<Page<MovementDto>> Movements(string actorId, Guid? productId = null, int page = 1, int size = 20, CancellationToken ct = default)
    {
        (await Actor(actorId, ct)).Demand(Roles.Manager, Roles.Inventory);
        var pg = Paging(page, size);
        var q = from m in db.Movements.AsNoTracking() join p in db.Products on m.ProductId equals p.Id join doc in db.Documents on m.DocumentId equals doc.Id into docs from d in docs.DefaultIfEmpty() where productId == null || m.ProductId == productId select new { m, p.Sku, p.Name, Number = d == null ? null : d.Number };
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.m.CreatedUtc).ThenBy(x => x.m.Id).Skip((pg.Page - 1) * pg.Size).Take(pg.Size).Select(x => new MovementDto(x.m.Id, x.m.ProductId, x.Sku, x.Name, x.m.DocumentId, x.Number, x.m.Before, x.m.Change, x.m.After, x.m.Type, x.m.Reason, x.m.ActorId, x.m.CreatedUtc)).ToListAsync(ct);
        return new Page<MovementDto>(rows, total, pg.Page, pg.Size);
    }
    public async Task<Page<AuditDto>> AuditHistory(string actorId, int page = 1, int size = 20, CancellationToken ct = default)
    {
        (await Actor(actorId, ct)).Demand(Roles.Manager);
        var pg = Paging(page, size);
        var q = db.Activities.AsNoTracking();
        var total = await q.CountAsync(ct);
        return new Page<AuditDto>(await q.OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Id).Skip((pg.Page - 1) * pg.Size).Take(pg.Size).Select(x => new AuditDto(x.Id, x.ActorId, x.Action, x.EntityType, x.EntityId, x.Summary, x.CreatedUtc)).ToListAsync(ct), total, pg.Page, pg.Size);
    }
}
