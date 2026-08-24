using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Catalog.Application.Domain.Events;

public sealed record ProductCreatedDomainEvent(Guid ProductId, string Name, decimal Price) : DomainEvent;
public sealed record ProductPriceChangedDomainEvent(Guid ProductId, decimal NewPrice) : DomainEvent;
public sealed record ProductStockAdjustedDomainEvent(Guid ProductId, int NewStock) : DomainEvent;
public sealed record ProductReservedDomainEvent(Guid ProductId, int Quantity, Guid ReservationId) : DomainEvent;
public sealed record ProductReservationReleasedDomainEvent(Guid ProductId, int Quantity, Guid ReservationId) : DomainEvent;
public sealed record ProductDeactivatedDomainEvent(Guid ProductId) : DomainEvent;
