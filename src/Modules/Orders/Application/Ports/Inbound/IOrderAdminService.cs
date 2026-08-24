using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Orders.Application.Ports.Inbound;

/// <summary>Admin-owned operations for the Orders context (ADR-0007 amendment).</summary>
public interface IOrderAdminService
{
    Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
}
