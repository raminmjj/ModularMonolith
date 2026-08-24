using HotChocolate.Authorization;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;

/// <summary>Shared Result → GraphQL error mapping for all admin resolvers.</summary>
internal static class AdminResultExtensions
{
    public static T ThrowIfFailure<T>(this ModularMonolith.Framework.Results.Result<T> result)
    {
        if (result.IsSuccess) return result.Value!;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(result.Error!.Message).SetCode(result.Error.Code).Build());
    }

    public static void ThrowIfFailure(this Result result)
    {
        if (result.IsSuccess) return;
        throw new GraphQLException(
            ErrorBuilder.New().SetMessage(result.Error!.Message).SetCode(result.Error.Code).Build());
    }
}

// ─── QUERY ROOT EXTENSIONS (modular per domain — ADR-0007, Decision #1) ───

[ExtendObjectType("Query")]
public sealed class AdminCatalogQueries
{
    /// <summary>Products with optional search + inactive inclusion. Offset-paged.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<ProductAdminRowDto>> Products(
        string? search, bool includeInactive, int page, int pageSize,
        [Service] IProductReadDataProvider products, CancellationToken ct)
        => (await products.GetProductRowsAsync(search, includeInactive, page, pageSize, ct)).ThrowIfFailure();
}

[ExtendObjectType("Query")]
public sealed class AdminCustomerQueries
{
    /// <summary>Customer search by name, optional status filter.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<CustomerSnapshot>> Customers(
        string? search, string? status, int page, int pageSize,
        [Service] ICustomerAdminReadProvider customers, CancellationToken ct)
        => (await customers.SearchCustomersAsync(search, status, page, pageSize, ct)).ThrowIfFailure();

    /// <summary>Customer detail: profile + order history + payments (3 batched calls).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<CustomerDetail> Customer(
        Guid id, [Service] IAdminCustomerService service, CancellationToken ct)
        => (await service.GetDetailAsync(id, ct)).ThrowIfFailure();
}

[ExtendObjectType("Query")]
public sealed class AdminOrderQueries
{
    /// <summary>Orders with date-range and status filters.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<OrderAdminRowDto>> Orders(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize,
        [Service] IOrderReadDataProvider orders, CancellationToken ct)
        => (await orders.ListOrdersAsync(from, to, status, page, pageSize, ct)).ThrowIfFailure();

    /// <summary>Order detail incl. latest payment status.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<OrderDetail> Order(
        Guid id, [Service] IAdminOrderService service, CancellationToken ct)
        => (await service.GetDetailAsync(id, ct)).ThrowIfFailure();
}

[ExtendObjectType("Query")]
public sealed class AdminPaymentQueries
{
    /// <summary>All payments with filters.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<PaymentAdminRowDto>> Payments(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize,
        [Service] IPaymentAdminReadProvider payments, CancellationToken ct)
        => (await payments.ListPaymentsAsync(from, to, status, page, pageSize, ct)).ThrowIfFailure();
}

[ExtendObjectType("Query")]
public sealed class AdminAnalyticsQueries
{
    /// <summary>Sales summary over captured/refunded/failed payments (30s cached).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<SalesSummaryDto> SalesSummary(
        DateTimeOffset from, DateTimeOffset to,
        [Service] IAdminAnalyticsService service, CancellationToken ct)
        => (await service.GetSalesSummaryAsync(from, to, ct)).ThrowIfFailure();

    /// <summary>Top customers by captured spend in range.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<TopCustomerDto>> TopCustomers(
        DateTimeOffset from, DateTimeOffset to, int take,
        [Service] IAdminAnalyticsService service, CancellationToken ct)
        => (await service.GetTopCustomersAsync(from, to, take, ct)).ThrowIfFailure();

    /// <summary>Top products by units sold in range.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<TopProductDto>> TopProducts(
        DateTimeOffset from, DateTimeOffset to, int take,
        [Service] IAdminAnalyticsService service, CancellationToken ct)
        => (await service.GetTopProductsAsync(from, to, take, ct)).ThrowIfFailure();
}
