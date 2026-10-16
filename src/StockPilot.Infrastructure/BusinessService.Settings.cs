using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;

public sealed partial class BusinessService
{
    public async Task<SettingsInput> Settings(string actorId, CancellationToken ct = default)
    {
        await Actor(actorId, ct);
        var s = await db.Settings.AsNoTracking().SingleOrDefaultAsync(ct);
        return new SettingsInput(s?.Name ?? "StockPilot", s?.CurrencyCode ?? "");
    }
    public Task<Guid> SaveSettings(string actorId, SettingsInput input, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand();
        var name = Text(input.Name, "Business name", 160, true);
        var currency = Text(input.CurrencyCode, "Currency", 3, true).ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(c => c < 'A' || c > 'Z'))
            throw new BusinessException("Use the three-letter code for your ledger currency; verify it with your accountant.");
        var setting = await db.Settings.SingleOrDefaultAsync(ct);
        if (await db.Documents.AnyAsync(ct) && (setting == null || setting.CurrencyCode != currency))
            throw new BusinessException("Currency is immutable once any document exists. A deliberate accounting migration is required.", 409);
        if (setting == null)
        {
            setting = new BusinessSetting { Id = BusinessSetting.SingletonId };
            db.Settings.Add(setting);
        }
        setting.Name = name;
        setting.CurrencyCode = currency;
        Audit(actorId, "BusinessSettingsChanged", "BusinessSetting", setting.Id);
        return setting.Id;
    }, ct);
}
