using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Modules.Brand.Application.Domain.Events;
using ModularMonolith.Modules.Brand.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Brand.Application.Domain.Brands;

/// <summary>
/// Brand aggregate — brand identity + approval lifecycle.
/// Does NOT track supplying suppliers: the BrandSupplyAgreement relationship is
/// owned by the Supplier aggregate (ADR-0009) — mirroring it here would be
/// double bookkeeping of the same fact.
/// </summary>
public sealed class Brand : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public Slug Slug { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string CountryOfOrigin { get; private set; } = null!;
    public BrandStatus Status { get; private set; } = null!;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Brand() { }

    public static Brand Create(string name, string slug, string? description, string? logoUrl, string countryOfOrigin)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("BRAND_NAME_REQUIRED", "Brand name is required.");

        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug is null ? Slug.FromName(name) : Slug.Create(slug),
            Description = description?.Trim(),
            LogoUrl = logoUrl?.Trim(),
            CountryOfOrigin = ValidateCountry(countryOfOrigin),
            Status = BrandStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        brand.Raise(new BrandCreatedDomainEvent(brand.Id, brand.Name));
        return brand;
    }

    /// <summary>PendingReview → Approved.</summary>
    public void Approve()
    {
        if (Status == BrandStatus.Approved) return;
        if (Status != BrandStatus.PendingReview)
            throw new DomainException("BRAND_NOT_PENDING", $"Only pending brands can be approved (was '{Status.Value}').");

        Status = BrandStatus.Approved;
        RejectionReason = null;
        Raise(new BrandApprovedDomainEvent(Id));
    }

    /// <summary>PendingReview → Rejected with a mandatory reason.</summary>
    public void Reject(string reason)
    {
        if (Status == BrandStatus.Rejected) return;
        if (Status != BrandStatus.PendingReview)
            throw new DomainException("BRAND_NOT_PENDING", $"Only pending brands can be rejected (was '{Status.Value}').");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("REJECTION_REASON_REQUIRED", "A rejection reason is required.");

        Status = BrandStatus.Rejected;
        RejectionReason = reason.Trim();
        Raise(new BrandRejectedDomainEvent(Id, RejectionReason));
    }

    public void UpdateDetails(string name, string? description, string? logoUrl, string countryOfOrigin)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("BRAND_NAME_REQUIRED", "Brand name is required.");
        Name = name.Trim();
        Description = description?.Trim();
        LogoUrl = logoUrl?.Trim();
        CountryOfOrigin = ValidateCountry(countryOfOrigin);
        Raise(new BrandUpdatedDomainEvent(Id));
    }

    private static string ValidateCountry(string country)
    {
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new DomainException("BRAND_COUNTRY_INVALID", "Country must be an ISO-3166 alpha-2 code.");
        return country.ToUpperInvariant();
    }
}
