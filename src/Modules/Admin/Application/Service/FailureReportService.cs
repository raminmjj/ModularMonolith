using Microsoft.Extensions.Caching.Memory;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Application.Service;

/// <summary>
/// Composes the failed-payments report from TWO read sides:
///   1. one SQL over Payment (flat failed rows, filtered server-side),
///   2. ONE batched SQL over Customer for all customer ids on the page.
/// Composition (grouping, status filter, paging) happens HERE — pure LINQ over
/// flat DTOs. No N+1: exactly two provider calls regardless of group count.
/// Results are cached briefly — admin reports are read-heavy and tolerate staleness.
/// </summary>
public sealed class FailureReportService : Ports.Inbound.IFailureReportService
{
    private const int MaxRows = 5000;          // hard guard against unbounded scans
    private const int MaxPageSize = 100;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IPaymentReadDataProvider _payments;
    private readonly ICustomerReadDataProvider _customers;
    private readonly IOrderReadDataProvider _orders;
    private readonly IMemoryCache _cache;

    public FailureReportService(IPaymentReadDataProvider payments, ICustomerReadDataProvider customers,
        IOrderReadDataProvider orders, IMemoryCache cache)
        => (_payments, _customers, _orders, _cache) = (payments, customers, orders, cache);

    public async Task<Result<FailureReportPage>> GetCustomersWithFailedPaymentsAsync(FailureReportFilter filter, CancellationToken ct = default)
    {
        var validation = Validate(filter);
        if (validation is not null) return Result.Failure<FailureReportPage>(validation);

        var cacheKey = $"report:failures:{filter.From:o}:{filter.To:o}:{filter.MinAmount}:{filter.CustomerStatus}:{filter.Page}:{filter.PageSize}";
        if (_cache.TryGetValue(cacheKey, out FailureReportPage? cached) && cached is not null)
            return Result.Success(cached);

        // 1. Payment read side — single filtered SQL.
        var failures = await _payments.GetFailedPaymentsAsync(filter.From, filter.To, filter.MinAmount, ct);
        if (!failures.IsSuccess) return Result.Failure<FailureReportPage>(failures.Error);
        var rows = failures.Value!
            .OrderByDescending(f => f.FailedAt)
            .Take(MaxRows)
            .ToList();

        // 2. Group per customer (composition logic lives in THIS hexagon).
        CurrencyMismatchGuard(rows);
        var groups = rows
            .GroupBy(f => f.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Sum(f => f.Amount),
                Currency = g.First().Currency,
                Items = g.ToList(),
            })
            .ToList();

        // 3. Customer read side — ONE batched query for every customer on the report.
        var ids = groups.Select(g => g.CustomerId).ToList();
        var customersResult = await _customers.GetCustomersByIdsAsync(ids, ct);
        if (!customersResult.IsSuccess) return Result.Failure<FailureReportPage>(customersResult.Error);
        var customersById = customersResult.Value!.ToDictionary(c => c.Id);

        // 4. Join + optional status filter + page. Snapshots are wrapped in the
        //    report node (LastOrder enriched in step 5).
        var entries = groups
            .Where(g => customersById.ContainsKey(g.CustomerId))
            .Select(g =>
            {
                var s = customersById[g.CustomerId];
                return new CustomerFailureReportEntry(
                    new CustomerReportNode(s.Id, s.IdentityUserId, s.DisplayName, s.Status),
                    g.Total, g.Currency, g.Items.Count,
                    (IReadOnlyList<FailedPaymentDto>)g.Items);
            })
            .Where(e => filter.CustomerStatus is null
                        || string.Equals(e.Customer.Status, filter.CustomerStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var page = new FailureReportPage(
            [.. entries.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)],
            entries.Count, filter.Page, filter.PageSize);

        // 5. Orders read side — last order per FINAL-page customer (≤ PageSize ids),
        //    ONE batched call regardless of page size.
        //    IDENTITY TRANSLATION: Orders keys orders by IdentityUserId, so we map
        //    Customer.Id → IdentityUserId before the call and back afterwards.
        if (page.Items.Count > 0)
        {
            var identityByCustomerId = page.Items.ToDictionary(i => i.Customer.Id, i => i.Customer.IdentityUserId);
            var lastOrdersResult = await _orders.GetLastOrdersByCustomerAsync([.. identityByCustomerId.Values], ct);
            if (!lastOrdersResult.IsSuccess) return Result.Failure<FailureReportPage>(lastOrdersResult.Error);
            var lastOrderByIdentity = lastOrdersResult.Value!.ToDictionary(o => o.CustomerId);

            page = page with
            {
                Items = [.. page.Items.Select(i =>
                    i with
                    {
                        Customer = i.Customer with
                        {
                            LastOrder = lastOrderByIdentity.GetValueOrDefault(identityByCustomerId[i.Customer.Id]),
                        },
                    })],
            };
        }

        _cache.Set(cacheKey, page, CacheTtl);
        return Result.Success(page);
    }

    private static Error? Validate(FailureReportFilter filter)
    {
        if (filter.Page < 1) return new Error("REPORT_PAGE_INVALID", "Page must be >= 1.");
        if (filter.PageSize is < 1 or > MaxPageSize) return new Error("REPORT_PAGE_SIZE_INVALID", $"PageSize must be between 1 and {MaxPageSize}.");
        if (filter.From is not null && filter.To is not null && filter.From > filter.To)
            return new Error("REPORT_RANGE_INVALID", "'From' must not be after 'To'.");
        if (filter.MinAmount is < 0) return new Error("REPORT_MINAMOUNT_INVALID", "MinAmount must be >= 0.");
        if (filter.CustomerStatus is not null
            && !filter.CustomerStatus.Equals("Active", StringComparison.OrdinalIgnoreCase)
            && !filter.CustomerStatus.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            return new Error("REPORT_STATUS_INVALID", "CustomerStatus must be 'Active', 'Suspended' or omitted.");

        return null;
    }

    /// <summary>Refuse to sum across currencies — flag data problems instead of lying in totals.</summary>
    private static void CurrencyMismatchGuard(List<FailedPaymentDto> rows)
    {
        var currencies = rows.Select(r => r.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (currencies.Count > 1)
            throw new InvalidOperationException(
                $"Report spans multiple currencies ({string.Join(", ", currencies)}); totals would be misleading. Filter by currency first.");
    }
}
