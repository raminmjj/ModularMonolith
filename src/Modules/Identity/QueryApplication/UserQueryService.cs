using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Identity;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Identity.Application.Domain.Users;

using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Identity.QueryApplication;

public interface IUserQueryService
{
    Task<Result<UserSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
}

internal sealed class UserQueryService : IUserQueryService
{
    private readonly DbContext _db;
    public UserQueryService(IdentityDbContext db) => _db = db;

    public async Task<Result<UserSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _db.Set<User>()
            .Where(u => u.Id == id)
            .Select(u => new UserSnapshot(u.Id, u.Email.Value, u.DisplayName, u.IsActive))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<UserSnapshot>(new Error("USER_NOT_FOUND", "User was not found."))
            : Result.Success(result);
    }
}
