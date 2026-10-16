using System.ComponentModel.DataAnnotations;
namespace StockPilot.Core;
public sealed class BusinessSetting : Entity
{
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string Name { get; set; } = "StockPilot";
    public string CurrencyCode { get; set; } = "";
}
public sealed record SettingsInput([Required,StringLength(160)] string Name,[Required,RegularExpression("^[A-Za-z]{3}$")] string CurrencyCode);
