using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Payment.Application.Ports.Outbound;

/// <summary>
/// Transactional ACL port — Payment asks Customer synchronously within one logical
/// operation. NO events, NO message bus (ADR-0002/0005). Implemented by
/// CustomerGateway in Adapter/Outbound/Gateways, which delegates DIRECTLY to
/// Customer's ICustomerInboundPort. Uses ONLY Contracts DTOs.
///
/// NOTE: these are read-only queries against the provider; no compensation steps
/// are needed for them (nothing to roll back on Customer's side).
/// </summary>
public interface ICustomerGatewayPort
{
    Task<Result<CustomerStatusDto>> GetCustomerStatusAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default);
}
