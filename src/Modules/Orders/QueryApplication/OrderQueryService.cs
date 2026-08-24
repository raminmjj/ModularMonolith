using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Orders;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Orders.Application.Domain.Orders;
using ModularMonolith.Modules.Orders.Application.Domain.ValueObjects;

using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Orders.QueryApplication;

public interface IOrderQueryService
{
    Task<Result<OrderSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSnapshot>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Most recent order per customer for a batch of ids (cross-module report
    /// composition). Single SQL projection; latest-per-customer resolved here.
    /// No grouping semantics leak upward — flat rows only.
    /// </summary>
    Task<IReadOnlyList<LastOrderDto>> GetLastOrdersByCustomerAsync(IReadOnlyList<Guid> customerIds, CancellationToken ct = default);

    /// <summary>Flat order rows for the admin order list (single filtered SQL).</summary>
    Task<IReadOnlyList<OrderAdminRowDto>> ListOrdersAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Top products by units sold in range (single grouped SQL over denormalized lines).</summary>
    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken ct = default);
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

    public async Task<IReadOnlyList<LastOrderDto>> GetLastOrdersByCustomerAsync(IReadOnlyList<Guid> customerIds, CancellationToken ct = default)
    {
        if (customerIds.Count == 0) return [];

        var rows = await _db.Set<Order>()
            .Where(o => customerIds.Contains(o.UserId))
            .Select(o => new
            {
                o.Id,
                CustomerId = o.UserId,
                o.PlacedAt,
                o.TotalAmount,
                Status = o.Status.Value,
                ItemCount = o.Lines.Count,
            })
            .ToListAsync(ct);

        // Latest per customer (deterministic on ties by newest PlacedAt, then Id).
        IReadOnlyList<LastOrderDto> lastOrders = [.. rows
            .GroupBy(r => r.CustomerId)
            .Select(g => g.OrderByDescending(r => r.PlacedAt).ThenByDescending(r => r.Id).First())
            .Select(r => new LastOrderDto(r.Id, r.CustomerId, r.PlacedAt, r.TotalAmount, r.Status, r.ItemCount))];

        return lastOrders;
    }

    public async Task<IReadOnlyList<OrderAdminRowDto>> ListOrdersAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Set<Order>().AsQueryable();

        if (from is not null) query = query.Where(o => o.PlacedAt >= from);
        if (to is not null) query = query.Where(o => o.PlacedAt <= to);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status.Value == status);

        return await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderAdminRowDto(
                o.Id, o.UserId, o.PlacedAt, o.TotalAmount, o.Status.Value, o.Lines.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken ct = default)
    {
        var ordersInRange = _db.Set<Order>().AsQueryable();
        if (from is not null) ordersInRange = ordersInRange.Where(o => o.PlacedAt >= from);
        if (to is not null) ordersInRange = ordersInRange.Where(o => o.PlacedAt <= to);

        // OrderLine rows carry denormalized ProductName; OrderId is a SHADOW FK
        // (no CLR property) — accessed via EF.Property. Single grouped SQL.
        return await _db.Set<OrderLine>()
            .Join(ordersInRange,
                l => EF.Property<Guid>(l, "OrderId"),
                o => o.Id,
                (l, o) => new { l.ProductName, l.UnitPrice, l.Quantity })
            .GroupBy(x => x.ProductName)
            .OrderByDescending(g => g.Sum(x => x.Quantity))
            .Take(take)
            .Select(g => new TopProductDto(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPrice * x.Quantity)))
            .ToListAsync(ct);
    }
}
