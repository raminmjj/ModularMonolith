using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories;

internal sealed class UserRepository : EfCoreRepository<Application.Domain.Users.User, Guid>, Application.Ports.Outbound.IUserRepository
{
    private readonly IdentityDbContext _db;
    public UserRepository(IdentityDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Users.User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<Application.Domain.Users.User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var n = email.Trim().ToLowerInvariant();
        return await _db.Users.FirstOrDefaultAsync(u => u.Email.Value == n, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var n = email.Trim().ToLowerInvariant();
        return await _db.Users.AnyAsync(u => u.Email.Value == n, ct);
    }
}

internal sealed class IdentityUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<IdentityDbContext> _inner;
    public IdentityUnitOfWork(IdentityDbContext db) => _inner = new(db);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default)
        => _inner.ExecuteInTransactionAsync(action, ct);
}
