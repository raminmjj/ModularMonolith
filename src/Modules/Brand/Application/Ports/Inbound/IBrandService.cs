using ModularMonolith.Contracts.Brand;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Brand.Application.Ports.Inbound;

/// <summary>Public inbound port of the Brand hexagon.</summary>
public interface IBrandService
{
    Task<Result<Guid>> CreateAsync(string name, string? slug, string? description, string? logoUrl, string countryOfOrigin, CancellationToken ct = default);
    Task<Result<BrandSnapshot>> GetAsync(Guid brandId, CancellationToken ct = default);

    /// <summary>ACL-facing existence check (consumed by the Supplier module's checker).</summary>
    Task<Result> EnsureBrandExistsAsync(Guid brandId, CancellationToken ct = default);
}
