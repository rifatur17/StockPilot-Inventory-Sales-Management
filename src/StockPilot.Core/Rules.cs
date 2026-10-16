namespace StockPilot.Core;

public sealed class BusinessException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
public static class Roles
{
    public const string Admin = "Admin", Manager = "Manager", Sales = "SalesStaff", Inventory = "InventoryStaff";
    public static readonly string[] All = [Admin, Manager, Sales, Inventory];
}
public sealed record Actor(string Id, IReadOnlyCollection<string> Roles)
{
    public bool Has(params string[] roles) => Roles.Contains(Core.Roles.Admin) || roles.Any(Roles.Contains);
    public void Demand(params string[] roles)
    {
        if (!Has(roles))
            throw new BusinessException("You do not have permission for this operation.", 403);
    }
    public bool Supervises => Has(Core.Roles.Manager);
}
public static class Money
{
    public static decimal Round(decimal n) => decimal.Round(n, 2, MidpointRounding.AwayFromZero);
    public static void Amount(decimal n, string field, bool positive = false)
    {
        if (n < 0 || n > 999999999m || (positive && n == 0) || Round(n) != n)
            throw new BusinessException($"{field} must be {(positive ? "positive" : "nonnegative")}, at most 999999999, with at most 2 decimal places.");
    }
    public static void Quantity(decimal n)
    {
        if (n <= 0 || n > 1000000 || decimal.Round(n, 3) != n)
            throw new BusinessException("Quantity must be positive, at most 1000000, with at most 3 decimal places.");
    }
    public static (decimal Subtotal, decimal Tax, decimal Total) Totals(IEnumerable<decimal> lines, decimal discount, decimal taxRate)
    {
        Amount(discount, "Discount");
        if (taxRate < 0 || taxRate > 100 || decimal.Round(taxRate, 2) != taxRate)
            throw new BusinessException("Tax rate must be between 0 and 100, with at most 2 decimals.");
        var sub = lines.Sum(Round);
        if (sub > 999999999 || discount > sub)
            throw new BusinessException("Discount cannot exceed subtotal; document value must be at most 999999999.");
        var tax = Round((sub - discount) * taxRate / 100);
        var total = sub - discount + tax;
        Amount(total, "Total");
        return (sub, tax, total);
    }
    public static decimal Average(decimal stock, decimal cost, decimal received, decimal unitCost)
        => decimal.Round((stock * cost + received * unitCost) / (stock + received), 6, MidpointRounding.AwayFromZero);
    public static string PaymentState(decimal total, decimal paid, decimal refunded, bool reversed)
        => reversed ? (paid - refunded > 0 ? "RefundDue" : "Reversed") : refunded > 0 ? "Refunded" : total == 0 ? "Paid" : paid == 0 ? "Unpaid" : paid < total ? "PartiallyPaid" : "Paid";
}
