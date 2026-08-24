namespace ModularMonolith.Contracts.Catalog;

/// <summary>
/// Snapshot of a product for cross-module consumption. Used by Orders module
/// via transactional ACL gateway — NOT an integration event.
/// </summary>
public sealed record ProductSnapshot(Guid Id, string Name, decimal Price, int AvailableStock);

/// <summary>
/// Reservation snapshot returned by Catalog when Orders reserves stock.
/// Transactional — both modules participate in the same database transaction.
/// </summary>
public sealed record ReservationSnapshot(Guid ReservationId, Guid ProductId, int Quantity, DateTimeOffset ExpiresAt);
