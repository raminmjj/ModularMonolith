using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Orders;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Orders.Application.Domain.Orders;

using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Orders.QueryApplication;

public interface IOrderQueryService
{
    Task<Result<OrderSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSnapshot>> ListForUserAsync(Guid userId, CancellationToken ct = default);
}

internal sealed class OrderQueryService : IOrderQueryService
{
    private readonly DbContext _db;
    public OrderQueryService(OrdersDbContext db) => _db = db;

    public async Task<Result<OrderSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _db.Set<Order>()
            .Where(o => o.Id == id)
            .Select(o => new OrderSnapshot(o.Id, o.UserId, o.TotalAmount, o.Status.Value, o.PlacedAt))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<OrderSnapshot>(new Error("ORDER_NOT_FOUND", "Order was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<OrderSnapshot>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Set<Order>()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.PlacedAt)
            .Select(o => new OrderSnapshot(o.Id, o.UserId, o.TotalAmount, o.Status.Value, o.PlacedAt))
            .ToListAsync(ct);
}
