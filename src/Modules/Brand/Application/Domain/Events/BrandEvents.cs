using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Brand.Application.Domain.Events;

public sealed record BrandCreatedDomainEvent(Guid BrandId, string Name) : DomainEvent;
public sealed record BrandApprovedDomainEvent(Guid BrandId) : DomainEvent;
public sealed record BrandRejectedDomainEvent(Guid BrandId, string Reason) : DomainEvent;
public sealed record BrandUpdatedDomainEvent(Guid BrandId) : DomainEvent;
