namespace ModularMonolith.Contracts.Brand;

/// <summary>Read-side projection of a brand.</summary>
public sealed record BrandSnapshot(
    Guid Id, string Name, string Slug, string? Description,
    string? LogoUrl, string CountryOfOrigin, string Status);
