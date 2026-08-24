using ModularMonolith.Contracts.Customer;

namespace ModularMonolith.Contracts.Reporting;

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

/// <summary>Composed report entry: customer info + their aggregated failures.</summary>
public sealed record CustomerFailureReportEntry(
    CustomerSnapshot Customer,
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
