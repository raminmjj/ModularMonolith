using ModularMonolith.Contracts.Orders;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Orders.Application.Ports.Inbound;

public interface IOrderService
{
    Task<Result<OrderSnapshot>> PlaceOrderAsync(Guid userId, IEnumerable<(Guid ProductId, int Quantity)> lines, CancellationToken ct = default);
    Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
    Task<Result<OrderSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
}
