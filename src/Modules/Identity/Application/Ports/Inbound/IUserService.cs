using ModularMonolith.Framework.Results;
using ModularMonolith.Contracts.Identity;

namespace ModularMonolith.Modules.Identity.Application.Ports.Inbound;

public interface IUserService
{
    Task<Result<UserSnapshot>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default);
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<Result<LoginResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<UserSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
}

