using Microsoft.Extensions.Logging;
using ModularMonolith.Contracts.Identity;
using ModularMonolith.Framework.Common;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Identity.Application.Domain.Users;
using ModularMonolith.Modules.Identity.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Identity.Application.Ports.Inbound;
using ModularMonolith.Modules.Identity.Application.Ports.Outbound;
using ModularMonolith.Modules.Identity.Application.Validation;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Identity.Application.Service;

public sealed class UserService : Ports.Inbound.IUserService
{
    private readonly IUserRepository _users;
    private readonly Domain.Users.IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokens;
    private readonly IClock _clock;
    private readonly ILogger<UserService> _logger;
    private readonly RegisterValidator _registerValidator;
    private readonly LoginValidator _loginValidator;

    private static readonly Error EmailTaken = new("USER_EMAIL_TAKEN", "Email is already registered.");
    private static readonly Error InvalidCredentials = new("USER_INVALID_CREDENTIALS", "Email or password is incorrect.");
    private static readonly Error UserNotFound = new("USER_NOT_FOUND", "User was not found.");
    private static readonly Error UserLocked = new("USER_LOCKED", "User is temporarily locked.");
    private static readonly Error InvalidRefreshToken = new("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");

    public UserService(
        IUserRepository users,
        Domain.Users.IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        ITokenService tokens,
        IClock clock,
        ILogger<UserService> logger,
        RegisterValidator registerValidator,
        LoginValidator loginValidator)
    {
        _users = users; _hasher = hasher; _unitOfWork = unitOfWork; _tokens = tokens; _clock = clock;
        _logger = logger; _registerValidator = registerValidator; _loginValidator = loginValidator;
    }

    public async Task<Result<UserSnapshot>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default)
    {
        // Hexagon boundary validation
        var validationResult = await _registerValidator.ValidateAsync((email, password, displayName), ct);
        if (!validationResult.IsValid)
            return Result.Failure<UserSnapshot>(new Error("VALIDATION", string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))));

        var emailVo = Email.Create(email);
        if (await _users.EmailExistsAsync(emailVo.Value, ct))
            return Result.Failure<UserSnapshot>(EmailTaken);

        var (hash, salt) = _hasher.Hash(password);
        var user = User.Register(emailVo, HashedPassword.Create(hash, salt), displayName);

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToSnapshot(user));
    }

    public async Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        // Hexagon boundary validation
        var validationResult = await _loginValidator.ValidateAsync((email, password), ct);
        if (!validationResult.IsValid)
            return Result.Failure<LoginResult>(new Error("VALIDATION", string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))));

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalizedEmail, ct);
        if (user is null || !user.IsActive) return Result.Failure<LoginResult>(InvalidCredentials);
        if (user.IsLockedAt(_clock.UtcNow)) return Result.Failure<LoginResult>(UserLocked);

        if (!_hasher.Verify(password, user.Password.Hash, user.Password.Salt))
        {
            user.RecordFailedLogin(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Failure<LoginResult>(InvalidCredentials);
        }

        user.RecordSuccessfulLogin(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        var (accessToken, expiresAt) = _tokens.GenerateAccessToken(user);
        var (refreshToken, _) = _tokens.GenerateRefreshToken(user);

        return Result.Success(new LoginResult(accessToken, refreshToken, expiresAt, ToSnapshot(user)));
    }

    public async Task<Result<LoginResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (!_tokens.ValidateRefreshToken(refreshToken, out var userId))
            return Result.Failure<LoginResult>(InvalidRefreshToken);

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return Result.Failure<LoginResult>(InvalidRefreshToken);

        var (accessToken, expiresAt) = _tokens.GenerateAccessToken(user);
        var (newRefreshToken, _) = _tokens.GenerateRefreshToken(user);

        return Result.Success(new LoginResult(accessToken, newRefreshToken, expiresAt, ToSnapshot(user)));
    }

    public async Task<Result<UserSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null) return Result.Failure<UserSnapshot>(UserNotFound);
        return Result.Success(ToSnapshot(user));
    }

    private static UserSnapshot ToSnapshot(User user) =>
        new(user.Id, user.Email.Value, user.DisplayName, user.IsActive);
}
