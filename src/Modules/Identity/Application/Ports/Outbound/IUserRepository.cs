namespace ModularMonolith.Modules.Identity.Application.Ports.Outbound;

public interface IUserRepository
{
    Task<Domain.Users.User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Users.User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(Domain.Users.User aggregate, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}

/// <summary>Outbound port — token generation abstraction.</summary>
public interface ITokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) GenerateAccessToken(Domain.Users.User user);
    (string RefreshToken, DateTimeOffset ExpiresAt) GenerateRefreshToken(Domain.Users.User user);
    bool ValidateRefreshToken(string token, out Guid userId);
}
