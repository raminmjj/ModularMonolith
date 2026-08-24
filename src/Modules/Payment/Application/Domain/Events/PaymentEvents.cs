using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Payment.Application.Domain.Events;

/// <summary>Recorded for audit/observability. NO dispatcher exists by design (ADR-0004).</summary>
public sealed record PaymentInitiatedDomainEvent(Guid PaymentId, Guid CustomerId, decimal Amount, string Currency) : DomainEvent;
public sealed record PaymentCapturedDomainEvent(Guid PaymentId) : DomainEvent;
public sealed record PaymentFailedDomainEvent(Guid PaymentId, string Reason) : DomainEvent;
public sealed record PaymentRefundedDomainEvent(Guid PaymentId) : DomainEvent;
