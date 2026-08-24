using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Customer.Application.Ports.Inbound;

/// <summary>Admin-owned operations for the Customer context (ADR-0007 amendment).</summary>
public interface ICustomerAdminService
{
    Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default);
}
