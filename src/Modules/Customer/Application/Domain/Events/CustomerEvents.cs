using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Customer.Application.Domain.Events;

/// <summary>Recorded for audit/observability. NO dispatcher exists by design (ADR-0004).</summary>
public sealed record CustomerRegisteredDomainEvent(Guid CustomerId, Guid IdentityUserId) : DomainEvent;
public sealed record CustomerSuspendedDomainEvent(Guid CustomerId) : DomainEvent;
public sealed record CustomerReactivatedDomainEvent(Guid CustomerId) : DomainEvent;
public sealed record SavedPaymentMethodAddedDomainEvent(Guid CustomerId, Guid PaymentMethodId) : DomainEvent;
