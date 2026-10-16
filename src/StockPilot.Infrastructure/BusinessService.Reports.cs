using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;

public sealed partial class BusinessService
{
    public Task<ReportDto> Report(string actorId, DateTime? from = null, DateTime? to = null, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand(Roles.Manager);
        UtcDates.Validate(from ?? DateTime.UtcNow.Date.AddDays(-29), to ?? DateTime.UtcNow.Date);
        var start = DateTime.SpecifyKind((from ?? DateTime.UtcNow.Date.AddDays(-29)).Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind((to ?? DateTime.UtcNow.Date).Date.AddDays(1), DateTimeKind.Utc);
        if (end <= start || (end - start).TotalDays > 366)
            throw new BusinessException("Select an inclusive date range of 1–366 UTC days.");
        // Coherent, bounded document cohort. Current payment/return state is applied to the original completion date.
        var docs = await db.Documents.AsNoTracking().Include(x => x.Items).Where(x => x.CompletedUtc >= start && x.CompletedUtc < end && (x.State == DocumentState.Completed || x.State == DocumentState.Reversed)).OrderBy(x => x.CompletedUtc).Take(10001).ToListAsync(ct);
        if (docs.Count > 10000)
            throw new BusinessException("Range exceeds 10000 documents; choose a smaller date range.");
        var ids = docs.Select(x => x.Id).ToArray();
        var payments = await db.Payments.AsNoTracking().Where(x => ids.Contains(x.DocumentId)).ToListAsync(ct);
        var sales = docs.Where(x => x.Kind == DocumentKind.Sale && x.State == DocumentState.Completed).ToArray();
        var subtotal = sales.Sum(x => x.Subtotal);
        var discount = sales.Sum(x => x.Discount);
        var revenue = subtotal - discount;
        decimal RefundedRevenue(Document d) => d.Total == 0 ? 0 : Money.Round(payments.Where(p => p.DocumentId == d.Id && p.Refund).Sum(p => p.Amount) * (d.Subtotal - d.Discount) / d.Total);
        var refunds = payments.Where(x => x.Refund).Sum(x => x.Amount);
        var revenueRefunds = sales.Sum(RefundedRevenue);
        var cogs = sales.Sum(d => d.Items.Sum(i => Money.Round(i.Quantity * i.UnitCost)));
        var stock = await db.Products.AsNoTracking().Select(x => new { x.Stock, x.AverageCost, x.MinimumStock, x.Active }).ToListAsync(ct);
        var staffIds = sales.Select(x => x.ActorId).Distinct().ToArray();
        var staff = await db.Users.AsNoTracking().Where(x => staffIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var daily = sales.GroupBy(x => x.CompletedUtc!.Value.Date).OrderBy(x => x.Key).Select(g => new GroupTotal(g.Key.ToString("yyyy-MM-dd"), g.Sum(d => d.Subtotal - d.Discount - RefundedRevenue(d)), g.Count())).ToArray();
        var lines = sales.SelectMany(d => d.Items.Select(i => new { Item = i, Revenue = Money.Round(i.LineTotal * (d.Subtotal == 0 ? 1 : 1 - d.Discount / d.Subtotal)) })).ToArray();
        var products = lines.GroupBy(x => new { x.Item.ProductId, x.Item.Sku, x.Item.ProductName }).Select(g => new GroupTotal(g.Key.Sku + " · " + g.Key.ProductName, g.Sum(x => x.Revenue), g.Sum(x => x.Item.Quantity))).OrderByDescending(x => x.Quantity).Take(20).ToArray();
        var categories = lines.GroupBy(x => x.Item.CategoryName).Select(g => new GroupTotal(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.Item.Quantity))).OrderByDescending(x => x.Amount).ToArray();
        var methods = payments.GroupBy(x => x.Method).Select(g => new GroupTotal(g.Key.ToString(), g.Sum(x => x.Refund ? -x.Amount : x.Amount), g.Count())).OrderBy(x => x.Label).ToArray();
        var staffTotals = sales.GroupBy(x => x.ActorId).Select(g => new GroupTotal(staff.GetValueOrDefault(g.Key, g.Key), g.Sum(x => x.Subtotal - x.Discount - RefundedRevenue(x)), g.Count())).OrderByDescending(x => x.Amount).ToArray();
        return new ReportDto(start, end.AddDays(-1), subtotal, discount, sales.Sum(x => x.Tax), revenue, refunds, revenue - revenueRefunds, cogs, revenue - revenueRefunds - cogs, docs.Where(x => x.Kind == DocumentKind.Purchase && x.State == DocumentState.Completed).Sum(x => x.Total), Money.Round(stock.Sum(x => x.Stock * x.AverageCost)), sales.Sum(d => Math.Max(0, d.Total - payments.Where(p => p.DocumentId == d.Id && !p.Refund).Sum(p => p.Amount))), stock.Count(x => x.Active && x.Stock <= x.MinimumStock), stock.Count(x => x.Active && x.Stock == 0), daily, products, categories, methods, staffTotals);
    }, ct);
}
