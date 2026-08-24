using Microsoft.Extensions.Caching.Memory;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Reporting.Application.Service;

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
    private readonly IMemoryCache _cache;

    public FailureReportService(IPaymentReadDataProvider payments, ICustomerReadDataProvider customers, IMemoryCache cache)
        => (_payments, _customers, _cache) = (payments, customers, cache);

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

        // 4. Join + optional status filter + page.
        var entries = groups
            .Where(g => customersById.ContainsKey(g.CustomerId))
            .Select(g => new CustomerFailureReportEntry(
                customersById[g.CustomerId], g.Total, g.Currency, g.Items.Count,
                (IReadOnlyList<FailedPaymentDto>)g.Items))
            .Where(e => filter.CustomerStatus is null
                        || string.Equals(e.Customer.Status, filter.CustomerStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var page = new FailureReportPage(
            [.. entries.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)],
            entries.Count, filter.Page, filter.PageSize);

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
