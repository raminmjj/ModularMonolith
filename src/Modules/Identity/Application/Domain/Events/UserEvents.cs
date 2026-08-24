using ModularMonolith.DDD.Events;

namespace ModularMonolith.Modules.Identity.Application.Domain.Events;

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email, string DisplayName) : DomainEvent;
public sealed record UserRoleAssignedDomainEvent(Guid UserId, string Role) : DomainEvent;
public sealed record UserPasswordChangedDomainEvent(Guid UserId) : DomainEvent;
