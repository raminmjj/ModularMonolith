using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Identity.Application.Domain.Events;
using ModularMonolith.Modules.Identity.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Identity.Application.Domain.Users;

public sealed class User : AggregateRoot<Guid>
{
    public Email Email { get; private set; } = null!;
    public HashedPassword Password { get; private set; } = null!;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    private readonly List<string> _roles = new();
    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();

    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly string[] DefaultRoles = { "Customer" };

    private User() { }

    public static User Register(Email email, HashedPassword password, string displayName, IEnumerable<string>? roles = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("USER_DISPLAY_NAME_EMPTY", "Display name cannot be empty.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = password,
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var roleList = (roles ?? DefaultRoles).Distinct().ToList();
        user._roles.AddRange(roleList);

        user.Raise(new UserRegisteredDomainEvent(user.Id, email.Value, user.DisplayName));
        foreach (var role in roleList)
            user.Raise(new UserRoleAssignedDomainEvent(user.Id, role));

        return user;
    }

    public void ChangePassword(HashedPassword newPassword)
    {
        Password = newPassword;
        Raise(new UserPasswordChangedDomainEvent(Id));
    }

    public void AssignRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new DomainException("ROLE_EMPTY", "Role cannot be empty.");
        if (_roles.Contains(role)) return;
        _roles.Add(role);
        Raise(new UserRoleAssignedDomainEvent(Id, role));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
    }

    public void RecordSuccessfulLogin(DateTimeOffset when)
    {
        if (!IsActive) throw new DomainException("USER_INACTIVE", "Inactive user cannot log in.");
        if (LockedUntil is not null && LockedUntil > when)
            throw new DomainException("USER_LOCKED", $"User is locked until {LockedUntil:o}.");
        LastLoginAt = when;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public bool RecordFailedLogin(DateTimeOffset when)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            LockedUntil = when + LockoutDuration;
            return true;
        }
        return false;
    }

    public bool IsLockedAt(DateTimeOffset when) => LockedUntil is not null && LockedUntil > when;
}

/// <summary>Domain service port — password hashing algorithm abstraction.</summary>
public interface IPasswordHasher
{
    (string Hash, string Salt) Hash(string plaintext);
    bool Verify(string plaintext, string hash, string salt);
}
