using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

/// <summary>
/// Outbound port over the Payment READ side. Implemented in Adapter/Outbound by
/// delegating to Payment.QueryApplication — never to a write-side Application.
/// </summary>
public interface IPaymentReadDataProvider
{
    Task<Result<IReadOnlyList<FailedPaymentDto>>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default);
}

/// <summary>Outbound port over the Customer READ side (batch fetch, single SQL).</summary>
public interface ICustomerReadDataProvider
{
    Task<Result<IReadOnlyList<CustomerSnapshot>>> GetCustomersByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

/// <summary>Outbound port over the Orders READ side (batched last-order-per-user).</summary>
public interface IOrderReadDataProvider
{
    /// <param name="userIds">Identity user ids — the key the Orders module uses for order ownership.</param>
    Task<Result<IReadOnlyList<LastOrderDto>>> GetLastOrdersByCustomerAsync(IReadOnlyList<Guid> userIds, CancellationToken ct = default);

    /// <summary>Flat order rows for the admin order list (single filtered SQL).</summary>
    Task<Result<IReadOnlyList<OrderAdminRowDto>>> ListOrdersAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Top products by units sold in range (single grouped SQL over denormalized lines).</summary>
    Task<Result<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(
        DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken ct = default);
}

/// <summary>Admin reads over the Payment read side.</summary>
public interface IPaymentAdminReadProvider
{
    Task<Result<IReadOnlyList<PaymentAdminRowDto>>> ListPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<Result<ModularMonolith.Contracts.Payment.PaymentSnapshot>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>Admin reads over the Customer read side.</summary>
public interface ICustomerAdminReadProvider
{
    Task<Result<IReadOnlyList<CustomerSnapshot>>> SearchCustomersAsync(
        string? search, string? status, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Catalog read rows for the admin panel.</summary>
public interface IProductReadDataProvider
{
    Task<Result<IReadOnlyList<ProductAdminRowDto>>> GetProductRowsAsync(
        string? search, bool includeInactive, int page, int pageSize, CancellationToken ct = default);
}
