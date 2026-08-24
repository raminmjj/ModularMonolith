using Microsoft.Extensions.Logging;
using ModularMonolith.Contracts.Catalog;
using ModularMonolith.Contracts.Orders;
using ModularMonolith.Framework.Results;
using ModularMonolith.Framework.Sagas;
using ModularMonolith.Modules.Orders.Application.Domain.Orders;
using ModularMonolith.Modules.Orders.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Orders.Application.Service;

/// <summary>
/// Inbound port implementation. Orchestrates:
/// 1. Reserve stock via Catalog (ACL — direct call, not event)
/// 2. Place order with reservation IDs
/// 3. On confirm: commit reservations (stock permanently deducted)
/// 4. On cancel: release reservations (stock returns)
///
/// CONSISTENCY SEMANTICS (docs/adr/0005): modules use separate databases, so this
/// is a synchronous SAGA, not an atomic transaction. If any step fails, previously
/// completed reservations are released via CrossModuleSaga compensation — logged
/// and metered if a release itself fails.
/// </summary>
public sealed class OrderService : Ports.Inbound.IOrderService
{
    private static readonly TimeSpan ReservationTtl = TimeSpan.FromMinutes(10);

    private readonly IOrderRepository _orders;
    private readonly ICatalogGateway _catalog;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orders, ICatalogGateway catalog, IUnitOfWork unitOfWork, ILogger<OrderService> logger)
    {
        _orders = orders; _catalog = catalog; _unitOfWork = unitOfWork; _logger = logger;
    }

    public async Task<Result<OrderSnapshot>> PlaceOrderAsync(
        Guid userId, IEnumerable<(Guid ProductId, int Quantity)> lines, CancellationToken ct = default)
    {
        var inputs = lines.ToList();
        var saga = new CrossModuleSaga(_logger, $"place-order:{userId}");

        var specs = new List<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, Guid ReservationId)>();
        Order? placedOrder = null;

        foreach (var input in inputs)
        {
            // Closure state per line: what this step reserved, so its compensation
            // releases exactly what it created (and nothing else).
            ProductSnapshot? snapshotValue = null;
            ReservationSnapshot? reservationValue = null;

            saga.Step(
                $"reserve-stock:{input.ProductId}",
                async token =>
                {
                    var product = await _catalog.GetProductAsync(input.ProductId, token);
                    if (!product.IsSuccess || product.Value is null)
                        return Result.Failure(new Error("PRODUCT_UNAVAILABLE", $"Product {input.ProductId} is unavailable."));
                    snapshotValue = product.Value;

                    var reservation = await _catalog.ReserveStockAsync(input.ProductId, input.Quantity, ReservationTtl, token);
                    if (!reservation.IsSuccess || reservation.Value is null)
                        return Result.Failure(reservation.Error ?? new Error("RESERVATION_FAILED", $"Could not reserve product {input.ProductId}."));

                    reservationValue = reservation.Value;
                    return Result.Success();
                },
                async token =>
                {
                    if (reservationValue is null) return; // step never completed — nothing to release
                    await _catalog.ReleaseReservationAsync(input.ProductId, reservationValue.ReservationId, token);
                });

            // Capture into `specs` as a separate compensated-free step so the mapping
            // cannot silently diverge from what was actually reserved.
            saga.Step(
                $"capture-line:{input.ProductId}",
                _ =>
                {
                    var sv = snapshotValue!;
                    var rv = reservationValue!;
                    specs.Add((sv.Id, sv.Name, sv.Price, input.Quantity, rv.ReservationId));
                    return Task.FromResult(Result.Success());
                });
        }

        saga.Step(
            "persist-order",
            async token =>
            {
                placedOrder = Order.Place(userId, specs);
                await _orders.AddAsync(placedOrder, token);
                await _unitOfWork.SaveChangesAsync(token); // Orders DB only
                return Result.Success();
            });

        var sagaResult = await saga.ExecuteAsync(ct);
        if (sagaResult.IsFailure) return Result.Failure<OrderSnapshot>(sagaResult.Error);

        return Result.Success(ToSnapshot(placedOrder!));
    }

    public async Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null) return Result.Failure(new Error("ORDER_NOT_FOUND", "Order was not found."));

        var status = Domain.ValueObjects.OrderStatus.Parse(newStatus);

        if (status.Value == "Confirmed")
        {
            foreach (var (productId, reservationId) in order.GetReservationsToCommit())
            {
                var r = await _catalog.CommitReservationAsync(productId, reservationId, ct);
                if (!r.IsSuccess) return r;
            }
            order.Confirm();
        }
        else if (status.Value == "Cancelled")
        {
            foreach (var (productId, reservationId) in order.GetReservationsToRelease())
                await ReleaseQuietlyAsync(productId, reservationId, ct); // TTL reaper reclaims on failure
            order.Cancel();
        }
        else
        {
            switch (status.Value)
            {
                case "Shipped": order.Ship(); break;
                case "Delivered": order.Deliver(); break;
                default: return Result.Failure(new Error("ORDER_STATUS_UNSUPPORTED", $"Status '{status.Value}' is not directly settable."));
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<OrderSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null) return Result.Failure<OrderSnapshot>(new Error("ORDER_NOT_FOUND", "Order was not found."));
        return Result.Success(ToSnapshot(order));
    }

    /// <summary>Best-effort release with OBSERVED failures (never silently swallowed).</summary>
    private async Task ReleaseQuietlyAsync(Guid productId, Guid reservationId, CancellationToken ct)
    {
        try { await _catalog.ReleaseReservationAsync(productId, reservationId, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to release reservation {ReservationId} for product {ProductId} during cancel — Catalog TTL reaper will reclaim it.",
                reservationId, productId);
            SagasMetrics.CompensationFailed("cancel-order", $"release:{productId}");
        }
    }

    private static OrderSnapshot ToSnapshot(Order order) =>
        new(order.Id, order.UserId, order.TotalAmount, order.Status.Value, order.PlacedAt);
}
