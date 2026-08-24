using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories;

/// <summary>Driven adapter — implements ICustomerRepository (outbound port) with EF Core.</summary>
internal sealed class CustomerRepository : EfCoreRepository<Application.Domain.Customers.Customer, Guid>,
    Application.Ports.Outbound.ICustomerRepository
{
    private readonly CustomerDbContext _db;
    public CustomerRepository(CustomerDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Customers.Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsForIdentityUserAsync(Guid identityUserId, CancellationToken ct = default)
        => _db.Customers.AnyAsync(c => c.IdentityUserId == identityUserId, ct);

    public async Task AddAsync(Application.Domain.Customers.Customer aggregate, CancellationToken ct = default)
        => await _db.Customers.AddAsync(aggregate, ct);
}

internal sealed class CustomerUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<CustomerDbContext> _inner;
    public CustomerUnitOfWork(CustomerDbContext db) => _inner = new EfCoreUnitOfWork<CustomerDbContext>(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default)
        => _inner.ExecuteInTransactionAsync(action, ct);
}
