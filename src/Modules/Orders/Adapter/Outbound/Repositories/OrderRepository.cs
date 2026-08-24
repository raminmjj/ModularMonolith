using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories;

internal sealed class OrderRepository : EfCoreRepository<Application.Domain.Orders.Order, Guid>, Application.Ports.Outbound.IOrderRepository
{
    private readonly OrdersDbContext _db;
    public OrderRepository(OrdersDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Application.Domain.Orders.Order>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Orders.Include(o => o.Lines)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.PlacedAt).ToListAsync(ct);
}

internal sealed class OrdersUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<OrdersDbContext> _inner;
    public OrdersUnitOfWork(OrdersDbContext db) => _inner = new(db);
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default)
        => _inner.ExecuteInTransactionAsync(action, ct);
}
