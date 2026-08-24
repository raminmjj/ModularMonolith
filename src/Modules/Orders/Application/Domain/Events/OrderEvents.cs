using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Orders.Application.Domain.Events;

public sealed record OrderPlacedDomainEvent(Guid OrderId, Guid UserId, decimal TotalAmount) : DomainEvent;
public sealed record OrderStatusChangedDomainEvent(Guid OrderId, string NewStatus) : DomainEvent;
