using ModularMonolith.Contracts.Admin;

namespace ModularMonolith.Contracts.Admin;

/// <summary>Flat product row for the admin catalog list (single SQL).</summary>
public sealed record ProductAdminRowDto(
    Guid Id, string Sku, string Name, decimal Price, string Currency,
    int Stock, int ReservedStock, int AvailableStock, bool IsActive);

/// <summary>Flat order row for the admin order list (single SQL).</summary>
public sealed record OrderAdminRowDto(
    Guid OrderId, Guid CustomerUserId, DateTimeOffset PlacedAt,
    decimal TotalAmount, string Status, int ItemCount);

/// <summary>Flat payment row for the admin payment list (single SQL).</summary>
public sealed record PaymentAdminRowDto(
    Guid PaymentId, Guid CustomerId, Guid OrderId, decimal Amount, string Currency,
    string Status, DateTimeOffset CreatedAt, string MethodToken);

/// <summary>Aggregated sales analytics over captured/failed payments in a range.</summary>
public sealed record SalesSummaryDto(
    DateTimeOffset From, DateTimeOffset To, string Currency,
    decimal TotalCapturedAmount, int CapturedCount,
    decimal TotalRefundedAmount, int RefundedCount,
    decimal TotalFailedAmount, int FailedCount,
    int PendingCount);

public sealed record TopCustomerDto(Guid CustomerId, string DisplayName, decimal TotalSpent, string Currency);
public sealed record TopProductDto(string ProductName, int QuantitySold, decimal Revenue);
