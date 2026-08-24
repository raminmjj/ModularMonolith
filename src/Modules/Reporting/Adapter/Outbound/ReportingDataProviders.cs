using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.QueryApplication;
using ModularMonolith.Modules.Customer.QueryApplication;
using ModularMonolith.Modules.Orders.QueryApplication;
using ModularMonolith.Modules.Payment.QueryApplication;
using ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Reporting.Adapter.Outbound;

// ─── READ-side ACL adapters ───
// Pure delegation to provider QueryApplications: flat Contract DTOs in/out.
// One adapter per provider; admin read interfaces extend the same classes so the
// admin panel reuses exactly the same data path as reports.

internal sealed class PaymentReadDataProvider(IPaymentQueryService payments)
    : IPaymentReadDataProvider, IPaymentAdminReadProvider
{
    public async Task<Result<IReadOnlyList<FailedPaymentDto>>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default)
        => Result.Success(await payments.GetFailedPaymentsAsync(from, to, minAmount, ct));

    public async Task<Result<IReadOnlyList<PaymentAdminRowDto>>> ListPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await payments.ListPaymentsAsync(from, to, status, page, pageSize, ct));

    public async Task<Result<ModularMonolith.Contracts.Payment.PaymentSnapshot>> GetByOrderIdAsync(
        Guid orderId, CancellationToken ct = default)
        => await payments.GetByOrderIdAsync(orderId, ct);
}

internal sealed class CustomerReadDataProvider(ICustomerQueryService customers)
    : ICustomerReadDataProvider, ICustomerAdminReadProvider
{
    public async Task<Result<IReadOnlyList<CustomerSnapshot>>> GetCustomersByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Result.Success(await customers.GetByIdsAsync(ids, ct));

    public async Task<Result<IReadOnlyList<CustomerSnapshot>>> SearchCustomersAsync(
        string? search, string? status, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await customers.SearchCustomersAsync(search, status, page, pageSize, ct));
}

internal sealed class OrdersReadDataProvider(IOrderQueryService orders) : IOrderReadDataProvider
{
    public async Task<Result<IReadOnlyList<LastOrderDto>>> GetLastOrdersByCustomerAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct = default)
        => Result.Success(await orders.GetLastOrdersByCustomerAsync(userIds, ct));

    public async Task<Result<IReadOnlyList<OrderAdminRowDto>>> ListOrdersAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await orders.ListOrdersAsync(from, to, status, page, pageSize, ct));

    public async Task<Result<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(
        DateTimeOffset? from, DateTimeOffset? to, int take, CancellationToken ct = default)
        => Result.Success(await orders.GetTopProductsAsync(from, to, take, ct));
}

/// <summary>Catalog read rows for the admin panel.</summary>
public sealed class ProductReadDataProvider(IProductQueryService products) : IProductReadDataProvider
{
    public async Task<Result<IReadOnlyList<ProductAdminRowDto>>> GetProductRowsAsync(
        string? search, bool includeInactive, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await products.GetProductRowsAsync(search, includeInactive, page, pageSize, ct));
}
