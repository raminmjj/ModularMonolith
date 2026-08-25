using ModularMonolith.Contracts.Brand;
using ModularMonolith.Contracts.Supplier;

using ModularMonolith.Framework.Results;
namespace ModularMonolith.Modules.Admin.Application.Ports.Outbound;

// ─── Admin read providers for the Supplier & Brand read sides (ADR-0009) ───
// Flat rows only — brand-name enrichment for agreement rows happens in Admin.

public interface ISupplierReadDataProvider
{
    /// <summary>Flat supplier rows for the admin list (single filtered SQL).</summary>
    Task<Result<IReadOnlyList<SupplierSnapshot>>> ListSuppliersAsync(
        string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Agreement rows owned by one supplier (single SQL on the M2M children).</summary>
    Task<Result<IReadOnlyList<BrandAgreementRowDto>>> GetAgreementsBySupplierAsync(
        Guid supplierId, CancellationToken ct = default);
}

public interface IBrandReadDataProvider
{
    /// <summary>Flat brand rows for the admin list (single filtered SQL).</summary>
    Task<Result<IReadOnlyList<BrandSnapshot>>> ListBrandsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default);
}
