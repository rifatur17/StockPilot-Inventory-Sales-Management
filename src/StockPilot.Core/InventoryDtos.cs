namespace StockPilot.Core;
public sealed record MovementDto(Guid Id, Guid ProductId, string Sku, string ProductName, Guid? DocumentId, string? DocumentNumber, decimal Before, decimal Change, decimal After, string Type, string Reason, string ActorId, DateTime CreatedUtc);
public sealed record AuditDto(Guid Id, string ActorId, string Action, string EntityType, string EntityId, string Summary, DateTime CreatedUtc);
