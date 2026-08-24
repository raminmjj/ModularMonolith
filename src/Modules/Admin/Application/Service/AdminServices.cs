using Microsoft.Extensions.Caching.Memory;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Payment;
using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Application.Service;

// All admin services: thin orchestration over read-side ports + write-side gateway
// ports. Validation of report-style inputs mirrors FailureReportService; caching
// (30s) applies to expensive analytics only — CRUD reads stay live.

internal sealed class AdminCatalogService(
    IProductReadDataProvider productReads,
    ICatalogAdminProvider catalogWrites) : Ports.Inbound.IAdminCatalogService
{
    private const int MaxPageSize = 100;

    public async Task<Result<IReadOnlyList<ProductAdminRowDto>>> ListProductsAsync(
        string? search, bool includeInactive, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) return Result.Failure<IReadOnlyList<ProductAdminRowDto>>(new Error("PAGE_INVALID", "Page must be >= 1."));
        if (pageSize is < 1 or > MaxPageSize)
            return Result.Failure<IReadOnlyList<ProductAdminRowDto>>(new Error("PAGE_SIZE_INVALID", $"PageSize must be between 1 and {MaxPageSize}."));
        return await productReads.GetProductRowsAsync(search, includeInactive, page, pageSize, ct);
    }

    public Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency,
        int initialStock, string? description, CancellationToken ct = default)
        => catalogWrites.CreateProductAsync(sku, name, price, currency, initialStock, description, ct);

    public Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default)
        => catalogWrites.ChangePriceAsync(productId, newPrice, currency, ct);

    public Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default)
        => catalogWrites.AdjustStockAsync(productId, delta, ct);

    public Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default)
        => catalogWrites.DeactivateProductAsync(productId, ct);
}

internal sealed class AdminCustomerService(
    ICustomerReadDataProvider customerReadsByIds,
    ICustomerAdminReadProvider customerSearch,
    IOrderReadDataProvider orderReads,
    IPaymentAdminReadProvider paymentReads,
    ICustomerAdminProvider customerWrites) : Ports.Inbound.IAdminCustomerService
{
    public async Task<Result<IReadOnlyList<CustomerSnapshot>>> SearchAsync(
        string? search, string? status, int page, int pageSize, CancellationToken ct = default)
        => await customerSearch.SearchCustomersAsync(search, status, page, pageSize, ct);

    /// <summary>Composes profile + order history + payments. THREE batched calls total.</summary>
    public async Task<Result<CustomerDetail>> GetDetailAsync(Guid customerId, CancellationToken ct = default)
    {
        var customersResult = await customerReadsByIds.GetCustomersByIdsAsync([customerId], ct);
        if (!customersResult.IsSuccess) return Result.Failure<CustomerDetail>(customersResult.Error);
        var snapshot = customersResult.Value!.FirstOrDefault();
        if (snapshot is null)
            return Result.Failure<CustomerDetail>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));

        // Orders are keyed by IdentityUserId (see ADR-0006 amendment).
        var ordersResult = await orderReads.ListOrdersAsync(null, null, null, 1, 50, ct);
        if (!ordersResult.IsSuccess) return Result.Failure<CustomerDetail>(ordersResult.Error);
        var history = ordersResult.Value!.Where(o => o.CustomerUserId == snapshot.IdentityUserId).ToList();

        var paymentsResult = await paymentReads.ListPaymentsAsync(null, null, null, 1, 100, ct);
        if (!paymentsResult.IsSuccess) return Result.Failure<CustomerDetail>(paymentsResult.Error);
        var payments = paymentsResult.Value!.Where(p => p.CustomerId == customerId).ToList();

        IReadOnlyList<OrderAdminRowDto> orderRows = history;
        IReadOnlyList<PaymentSnapshot> paymentRows =
        [
            .. payments.Select(p => new PaymentSnapshot(
                p.PaymentId, p.CustomerId, p.OrderId, p.Amount, p.Currency, p.Status, p.MethodToken)),
        ];
        return Result.Success(new CustomerDetail(snapshot, orderRows, paymentRows));
    }

    public Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default)
        => customerWrites.SuspendAsync(customerId, ct);

    public Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default)
        => customerWrites.ReactivateAsync(customerId, ct);
}

internal sealed class AdminOrderService(
    IOrderReadDataProvider orderReads,
    IPaymentAdminReadProvider paymentReads,
    IOrdersAdminProvider orderWrites) : Ports.Inbound.IAdminOrderService
{
    public async Task<Result<IReadOnlyList<OrderAdminRowDto>>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
        => await orderReads.ListOrdersAsync(from, to, status, page, pageSize, ct);

    /// <summary>Header + latest payment for the order. TWO calls, constant.</summary>
    public async Task<Result<OrderDetail>> GetDetailAsync(Guid orderId, CancellationToken ct = default)
    {
        var ordersResult = await orderReads.ListOrdersAsync(null, null, null, 1, int.MaxValue, ct);
        if (!ordersResult.IsSuccess) return Result.Failure<OrderDetail>(ordersResult.Error);
        var row = ordersResult.Value!.FirstOrDefault(o => o.OrderId == orderId);
        if (row is null)
            return Result.Failure<OrderDetail>(new Error("ORDER_NOT_FOUND", "Order was not found."));

        var paymentResult = await paymentReads.GetByOrderIdAsync(orderId, ct);
        var payment = paymentResult.IsSuccess ? paymentResult.Value : null;

        return Result.Success(new OrderDetail(row, payment));
    }

    public Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
        => orderWrites.ChangeStatusAsync(orderId, newStatus, ct);
}

internal sealed class AdminPaymentService(
    IPaymentAdminReadProvider paymentReads,
    IPaymentAdminProvider paymentWrites) : Ports.Inbound.IAdminPaymentService
{
    public async Task<Result<IReadOnlyList<PaymentAdminRowDto>>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
        => await paymentReads.ListPaymentsAsync(from, to, status, page, pageSize, ct);

    public Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
        => paymentWrites.CaptureAsync(paymentId, ct);

    public Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
        => paymentWrites.RefundAsync(paymentId, ct);

    public Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
        => paymentWrites.FailAsync(paymentId, reason, ct);
}

/// <summary>
/// Sales analytics composed from flat payment rows (single pull per request,
/// capped) with a 30-second cache — analytics are read-heavy and tolerate staleness.
/// </summary>
internal sealed class AdminAnalyticsService(
    IPaymentAdminReadProvider paymentReads,
    ICustomerReadDataProvider customerReads,
    IOrderReadDataProvider orderReads,
    IMemoryCache cache) : Ports.Inbound.IAdminAnalyticsService
{
    private const int MaxRows = 5000;
    private const int MaxTake = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<Result<SalesSummaryDto>> GetSalesSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        if (from > to) return Result.Failure<SalesSummaryDto>(new Error("RANGE_INVALID", "'From' must not be after 'To'."));

        var key = $"admin:sales:{from:o}:{to:o}";
        if (cache.TryGetValue(key, out SalesSummaryDto? cached) && cached is not null)
            return Result.Success(cached);

        var payments = await paymentReads.ListPaymentsAsync(from, to, null, 1, MaxRows, ct);
        if (!payments.IsSuccess) return Result.Failure<SalesSummaryDto>(payments.Error);

        var rows = payments.Value!.ToList();
        CurrencyGuard(rows.Select(r => r.Currency));

        var summary = new SalesSummaryDto(
            from, to,
            rows.FirstOrDefault()?.Currency ?? "USD",
            rows.Where(r => r.Status == "Captured").Sum(r => r.Amount),
            rows.Count(r => r.Status == "Captured"),
            rows.Where(r => r.Status == "Refunded").Sum(r => r.Amount),
            rows.Count(r => r.Status == "Refunded"),
            rows.Where(r => r.Status == "Failed").Sum(r => r.Amount),
            rows.Count(r => r.Status == "Failed"),
            rows.Count(r => r.Status == "Pending"));

        cache.Set(key, summary, CacheTtl);
        return Result.Success(summary);
    }

    public async Task<Result<IReadOnlyList<TopCustomerDto>>> GetTopCustomersAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, MaxTake);
        var key = $"admin:topcust:{from:o}:{to:o}:{take}";
        if (cache.TryGetValue(key, out TopCustomerDto[]? cached) && cached is not null)
            return Result.Success<IReadOnlyList<TopCustomerDto>>(cached);

        var payments = await paymentReads.ListPaymentsAsync(from, to, "Captured", 1, MaxRows, ct);
        if (!payments.IsSuccess) return Result.Failure<IReadOnlyList<TopCustomerDto>>(payments.Error);

        var rows = payments.Value!.ToList();
        CurrencyGuard(rows.Select(r => r.Currency));

        var topRows = rows
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(p => p.Amount), g.First().Currency })
            .OrderByDescending(x => x.Total)
            .Take(take)
            .ToList();

        var names = await customerReads.GetCustomersByIdsAsync([.. topRows.Select(t => t.CustomerId)], ct);
        if (!names.IsSuccess) return Result.Failure<IReadOnlyList<TopCustomerDto>>(names.Error);
        var nameById = names.Value!.ToDictionary(c => c.Id, c => c.DisplayName);

        IReadOnlyList<TopCustomerDto> result = [.. topRows
            .Select(t => new TopCustomerDto(t.CustomerId, nameById.GetValueOrDefault(t.CustomerId, "(unknown)"), t.Total, t.Currency))];

        cache.Set(key, result, CacheTtl);
        return Result.Success(result);
    }

    public Task<Result<IReadOnlyList<TopProductDto>>> GetTopProductsAsync(DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default)
        => orderReads.GetTopProductsAsync(from, to, Math.Clamp(take, 1, MaxTake), ct);

    private static void CurrencyGuard(IEnumerable<string> currencies)
    {
        var distinct = currencies.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count > 1)
            throw new InvalidOperationException(
                $"Analytics span multiple currencies ({string.Join(", ", distinct)}); totals would be misleading.");
    }
}
