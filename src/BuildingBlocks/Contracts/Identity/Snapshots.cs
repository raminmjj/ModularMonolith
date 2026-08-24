namespace ModularMonolith.Contracts.Identity;

/// <summary>
/// Snapshot of a user for cross-module consumption. Used by Orders module
/// via transactional ACL gateway.
/// </summary>
public sealed record UserSnapshot(Guid Id, string Email, string DisplayName, bool IsActive);

/// <summary>Login/refresh outcome returned across the Identity inbound boundary.</summary>
public sealed record LoginResult(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserSnapshot User);
