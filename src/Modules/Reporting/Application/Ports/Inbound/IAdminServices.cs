using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Payment;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Reporting.Application.Ports.Inbound;

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

/// <summary>Inbound port for catalog admin (queries + mutations).</summary>
public interface IAdminCatalogService
{
    Task<Result<IReadOnlyList<ProductAdminRowDto>>> ListProductsAsync(string? search, bool includeInactive, int page, int pageSize, CancellationToken ct = default);
    Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default);
    Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default);
    Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default);
    Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default);
}

public interface IAdminCustomerService
{
    Task<Result<IReadOnlyList<CustomerSnapshot>>> SearchAsync(string? search, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<Result<CustomerDetail>> GetDetailAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default);
}

public interface IAdminOrderService
{
    Task<Result<IReadOnlyList<OrderAdminRowDto>>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<Result<OrderDetail>> GetDetailAsync(Guid orderId, CancellationToken ct = default);
    Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
}

public interface IAdminPaymentService
{
    Task<Result<IReadOnlyList<PaymentAdminRowDto>>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);
    Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
}

public interface IAdminAnalyticsService
{
    Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TopCustomerDto>>> GetTopCustomersAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
}
