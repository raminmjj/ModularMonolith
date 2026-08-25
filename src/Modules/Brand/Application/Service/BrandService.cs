using ModularMonolith.Contracts.Brand;
using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Application.Domain.Brands;
using ModularMonolith.Modules.Brand.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Brand.Application.Ports.Outbound;

using BrandAggregate = ModularMonolith.Modules.Brand.Application.Domain.Brands.Brand;
namespace ModularMonolith.Modules.Brand.Application.Service;

/// <summary>Public inbound port implementation.</summary>
public sealed class BrandService(
    IBrandRepository brands,
    IUnitOfWork unitOfWork,
    IEventDispatcher eventDispatcher) : Ports.Inbound.IBrandService
{
    public async Task<Result<Guid>> CreateAsync(string name, string? slug, string? description,
        string? logoUrl, string countryOfOrigin, CancellationToken ct = default)
    {
        BrandAggregate brand;
        try { brand = BrandAggregate.Create(name, slug, description, logoUrl, countryOfOrigin); }
        catch (DomainException dex) { return Result.Failure<Guid>(new Error(dex.Code, dex.Message)); }

        var normalized = brand.Slug.Value;
        if (await brands.SlugExistsAsync(normalized, ct))
            return Result.Failure<Guid>(new Error("BRAND_SLUG_TAKEN", $"Brand slug '{normalized}' already exists."));

        await brands.AddAsync(brand, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(brand, ct);
        return Result.Success(brand.Id);
    }

    public async Task<Result<BrandSnapshot>> GetAsync(Guid brandId, CancellationToken ct = default)
    {
        var b = await brands.GetByIdAsync(brandId, ct);
        return b is null
            ? Result.Failure<BrandSnapshot>(new Error("BRAND_NOT_FOUND", "Brand was not found."))
            : Result.Success(new BrandSnapshot(b.Id, b.Name, b.Slug.Value, b.Description, b.LogoUrl, b.CountryOfOrigin, b.Status.Value));
    }

    public async Task<Result> EnsureBrandExistsAsync(Guid brandId, CancellationToken ct = default)
    {
        var b = await brands.GetByIdAsync(brandId, ct);
        return b is null
            ? Result.Failure(new Error("BRAND_NOT_FOUND", $"Brand '{brandId}' was not found."))
            : Result.Success();
    }
}
