using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Orders.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Orders.Application.Service;

/// <summary>Admin facade over the Orders public service — delegation is deliberate (ADR-0007).</summary>
public sealed class OrderAdminService(IOrderService orders) : IOrderAdminService
{
    public Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
        => orders.ChangeStatusAsync(orderId, newStatus, ct);
}
