namespace StockPilot.Core;
public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); }
public sealed class Category : Entity { public string Name { get; set; } = ""; }
public sealed class Unit : Entity { public string Name { get; set; } = ""; }
public sealed class Product : Entity
{
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid CategoryId { get; set; }
    public Guid UnitId { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal AverageCost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinimumStock { get; set; }
    public bool Active { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class Partner : Entity
{
    public string Kind { get; set; } = "Customer";
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Address { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Active { get; set; } = true;
}
public enum DocumentState { Draft, Completed, Cancelled, Reversed }
public enum DocumentKind { Purchase, Sale }
public sealed class Document : Entity
{
    public DocumentKind Kind { get; set; }
    public DocumentState State { get; set; }
    public string Number { get; set; } = "";
    public string Reference { get; set; } = "";
    public Guid? PartnerId { get; set; }
    public string PartnerName { get; set; } = "Walk-in";
    public string ActorId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<DocumentItem> Items { get; set; } = [];
}
public sealed class DocumentItem : Entity
{
    public Guid DocumentId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
public enum PaymentMethod { Cash, Card, BankTransfer, MobileFinancialService }
public sealed class Payment : Entity
{
    public Guid DocumentId { get; set; }
    public decimal Amount { get; set; }
    public bool Refund { get; set; }
    public PaymentMethod Method { get; set; }
    public string Reason { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class StockMovement : Entity
{
    public Guid ProductId { get; set; }
    public Guid? DocumentId { get; set; }
    public decimal Before { get; set; }
    public decimal Change { get; set; }
    public decimal After { get; set; }
    public string Type { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class ActivityLog : Entity
{
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class Operation : Entity
{
    public string ActorId { get; set; } = "";
    public Guid Key { get; set; }
    public string Fingerprint { get; set; } = "";
    public Guid ResultId { get; set; }
}
