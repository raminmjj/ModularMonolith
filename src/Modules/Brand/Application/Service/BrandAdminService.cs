using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Application.Ports.Outbound;

using BrandAggregate = ModularMonolith.Modules.Brand.Application.Domain.Brands.Brand;
namespace ModularMonolith.Modules.Brand.Application.Service;

/// <summary>Admin operations for the Brand context (ADR-0007: domain-owned).</summary>
public sealed class BrandAdminService(
    IBrandRepository brands,
    IUnitOfWork unitOfWork,
    IEventDispatcher eventDispatcher) : Ports.Inbound.IBrandAdminService
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

    public async Task<Result> ApproveAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        if (!brand.IsSuccess) return brand;

        try { brand.Value!.Approve(); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(brand.Value!, ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid brandId, string reason, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        if (!brand.IsSuccess) return brand;

        try { brand.Value!.Reject(reason); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(brand.Value!, ct);
        return Result.Success();
    }

    public async Task<Result> UpdateDetailsAsync(Guid brandId, string name, string? description,
        string? logoUrl, string countryOfOrigin, CancellationToken ct = default)
    {
        var brand = await RequireBrandAsync(brandId, ct);
        if (!brand.IsSuccess) return brand;

        try { brand.Value!.UpdateDetails(name, description, logoUrl, countryOfOrigin); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(brand.Value!, ct);
        return Result.Success();
    }

    private async Task<Result<BrandAggregate>> RequireBrandAsync(Guid id, CancellationToken ct)
    {
        var b = await brands.GetByIdAsync(id, ct);
        return b is null
            ? Result.Failure<BrandAggregate>(new Error("BRAND_NOT_FOUND", "Brand was not found."))
            : Result.Success(b);
    }
}
