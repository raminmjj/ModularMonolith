namespace ModularMonolith.Contracts.Orders;

public sealed record OrderSnapshot(Guid Id, Guid UserId, decimal TotalAmount, string Status, DateTimeOffset PlacedOn);
