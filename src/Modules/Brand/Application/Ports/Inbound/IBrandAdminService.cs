using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Brand.Application.Ports.Inbound;

/// <summary>Admin-owned operations for the Brand context (ADR-0007 amendment).</summary>
public interface IBrandAdminService
{
    Task<Result<Guid>> CreateAsync(string name, string? slug, string? description, string? logoUrl, string countryOfOrigin, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid brandId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid brandId, string reason, CancellationToken ct = default);
    Task<Result> UpdateDetailsAsync(Guid brandId, string name, string? description, string? logoUrl, string countryOfOrigin, CancellationToken ct = default);
}
