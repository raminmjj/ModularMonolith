using ModularMonolith.Contracts.Customer;

namespace ModularMonolith.Contracts.Admin;

/// <summary>Filter for the admin failed-payments report. All fields optional except paging.</summary>
public sealed record FailureReportFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    decimal? MinAmount,
    string? CustomerStatus, // "Active" | "Suspended" | null (any)
    int Page = 1,
    int PageSize = 20);

/// <summary>Flat failed-payment row supplied by the Payment read side.</summary>
public sealed record FailedPaymentDto(
    Guid PaymentId,
    Guid CustomerId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    DateTimeOffset FailedAt,
    string? Reason);

/// <summary>Last (most recent) order of a customer — flat row supplied by the Orders read side.</summary>
public sealed record LastOrderDto(
    Guid OrderId,
    Guid CustomerId,
    DateTimeOffset OrderDate,
    decimal TotalAmount,
    string Status,
    int ItemCount);

/// <summary>
/// Customer node as exposed in report entries: profile fields from the Customer
/// read side PLUS enriched cross-module data (last order from Orders read side).
/// </summary>
public sealed record CustomerReportNode(
    Guid Id,
    Guid IdentityUserId,
    string DisplayName,
    string Status,
    LastOrderDto? LastOrder = null);

/// <summary>Composed report entry: customer info + their aggregated failures.</summary>
/// <param name="LastOrder">Most recent order, if any. Nullable = backward-compatible GraphQL addition.</param>
public sealed record CustomerFailureReportEntry(
    CustomerReportNode Customer,
    decimal TotalFailedAmount,
    string Currency,
    int FailureCount,
    IReadOnlyList<FailedPaymentDto> Failures);

/// <summary>Paged report result. TotalCount reflects POST-filter groups.</summary>
public sealed record FailureReportPage(
    IReadOnlyList<CustomerFailureReportEntry> Items,
    int TotalCount,
    int Page,
    int PageSize);
