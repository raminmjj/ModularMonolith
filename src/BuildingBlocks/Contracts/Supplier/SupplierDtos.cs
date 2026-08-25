namespace ModularMonolith.Contracts.Supplier;

/// <summary>Read-side projection of a supplier profile.</summary>
public sealed record SupplierSnapshot(
    Guid Id, string Name, string ContactEmail, string? PhoneNumber,
    string? Address, string Status, bool IsVerified);

/// <summary>
/// Flat row of one brand-supply agreement owned by the Supplier aggregate.
/// BrandId references the Brand module; enrichment happens in Admin composition.
/// </summary>
public sealed record BrandAgreementRowDto(
    Guid BrandId, decimal CommissionRate, DateTimeOffset AssignedAt, bool IsActive);
