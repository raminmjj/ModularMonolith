using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Payment;
using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Application.Ports.Inbound;

// ─── Admin panel services (ADR-0007) ───
// Each composes provider READ sides via ports and delegates writes via admin
// gateway ports. GraphQL resolvers stay IO-free shells over these.

public sealed record CustomerDetail(
    CustomerSnapshot Customer,
    IReadOnlyList<OrderAdminRowDto> Orders,
    IReadOnlyList<PaymentSnapshot> Payments);

public sealed record OrderDetail(
    OrderAdminRowDto Order,
    PaymentSnapshot? Payment);

/// <summary>
/// COMPOSITION-ONLY admin services (ADR-0007 amendment). Single-module operations
/// live in the domain modules' own admin ports (Catalog.ICatalogAdminService, …);
/// the Admin module reaches them through its outbound gateway ports. Only
/// cross-module composition remains here.
/// </summary>
public interface IAdminCustomerService
{
    /// <summary>Customer 360: profile + order history + payments (three read sides).</summary>
    Task<Result<CustomerDetail>> GetDetailAsync(Guid customerId, CancellationToken ct = default);
}

public interface IAdminOrderService
{
    /// <summary>Order + latest payment status (two read sides).</summary>
    Task<Result<OrderDetail>> GetDetailAsync(Guid orderId, CancellationToken ct = default);
}

public interface IAdminAnalyticsService
{
    Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TopCustomerDto>>> GetTopCustomersAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
}
