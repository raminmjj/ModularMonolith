using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound; // ← EXCEPTION #2 (csproj reference)
using ModularMonolith.Modules.Payment.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Gateways;

/// <summary>
/// ACL adapter — implements Payment's OWN ICustomerGatewayPort by delegating
/// DIRECTLY to Customer's ICustomerInboundPort. Same process, synchronous,
/// read-only queries against the provider — no compensation needed on failure.
/// NO events, NO message bus, NO Wolverine (ADR-0002/0005).
/// </summary>
internal sealed class CustomerGateway(ICustomerInboundPort customerPort) : ICustomerGatewayPort
{
    public Task<Result<CustomerStatusDto>> GetCustomerStatusAsync(Guid customerId, CancellationToken ct = default)
        => customerPort.GetStatusAsync(customerId, ct);

    public Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default)
        => customerPort.GetSavedPaymentMethodAsync(customerId, paymentMethodId, ct);
}
