using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Customer.Application.Ports.Inbound;

/// <summary>
/// Inbound port — the hexagon's driving boundary. Used by BOTH the REST adapter
/// and (cross-module) Payment.Adapter.Outbound.CustomerGateway via DIRECT METHOD
/// CALLS — no events, no message bus (ADR-0002/0005).
/// </summary>
public interface ICustomerInboundPort
{
    // Profile lifecycle (REST-facing)
    Task<Result<Guid>> RegisterAsync(Guid identityUserId, string displayName, CancellationToken ct = default);
    Task<Result> AddAddressAsync(Guid customerId, string street, string city, string postalCode, string country, CancellationToken ct = default);
    Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default);

    // Wallet (REST-facing) — vault tokens only
    Task<Result<Guid>> SavePaymentMethodAsync(Guid customerId, string tokenizedCard, string cardType, DateOnly expiry, CancellationToken ct = default);

    // Standing & wallet (ACL-facing — consumed by Payment)
    Task<Result<CustomerStatusDto>> GetStatusAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SavedPaymentMethodDto>>> GetSavedPaymentMethodsAsync(Guid customerId, CancellationToken ct = default);
}
