using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Supplier.Application.Domain.Events;

/// <summary>Recorded for audit/observability; dispatched via the pluggable seam (NoOp today — ADR-0004).</summary>
public sealed record SupplierCreatedDomainEvent(Guid SupplierId) : DomainEvent;
public sealed record SupplierVerifiedDomainEvent(Guid SupplierId) : DomainEvent;
public sealed record SupplierSuspendedDomainEvent(Guid SupplierId) : DomainEvent;
public sealed record SupplierProfileUpdatedDomainEvent(Guid SupplierId) : DomainEvent;
public sealed record BrandAgreementAssignedDomainEvent(Guid SupplierId, Guid BrandId, decimal CommissionRate) : DomainEvent;
public sealed record BrandAgreementRemovedDomainEvent(Guid SupplierId, Guid BrandId) : DomainEvent;
