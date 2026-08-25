using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Brand;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer;
using BrandAggregate = ModularMonolith.Modules.Brand.Application.Domain.Brands.Brand;

namespace ModularMonolith.Modules.Brand.QueryApplication;

public interface IBrandQueryService
{
    Task<Result<BrandSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BrandSnapshot>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Strictly read-only: direct EF Core projections (ADR-0008).</summary>
internal sealed class BrandQueryService(BrandDbContext db) : IBrandQueryService
{
    public async Task<Result<BrandSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<BrandAggregate>()
            .Where(b => b.Id == id)
            .Select(b => new BrandSnapshot(
                b.Id, b.Name, b.Slug.Value, b.Description, b.LogoUrl, b.CountryOfOrigin, b.Status.Value))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<BrandSnapshot>(new Error("BRAND_NOT_FOUND", "Brand was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<BrandSnapshot>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Set<BrandAggregate>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status.Value == status);

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new BrandSnapshot(
                b.Id, b.Name, b.Slug.Value, b.Description, b.LogoUrl, b.CountryOfOrigin, b.Status.Value))
            .ToListAsync(ct);
    }
}
