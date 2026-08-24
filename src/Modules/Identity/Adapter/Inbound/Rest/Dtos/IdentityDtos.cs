namespace ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Dtos;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, Guid UserId, string Email, string DisplayName);
public sealed record UserProfileResponse(Guid Id, string Email, string DisplayName, bool IsActive);
