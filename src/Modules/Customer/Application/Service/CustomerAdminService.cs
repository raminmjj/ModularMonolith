using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Customer.Application.Service;

/// <summary>Admin facade over the Customer inbound port — delegation is deliberate (ADR-0007).</summary>
public sealed class CustomerAdminService(ICustomerInboundPort customers) : ICustomerAdminService
{
    public Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default)
        => customers.SuspendAsync(customerId, ct);

    public Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default)
        => customers.ReactivateAsync(customerId, ct);
}
