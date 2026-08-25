using HotChocolate.Authorization;
using ModularMonolith.Contracts.Brand;
using ModularMonolith.Contracts.Supplier;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;

// ─── Supplier & Brand admin queries (ADR-0009) ───
// Thin shells: reads resolve through Admin-owned read provider ports — zero IO here.

[ExtendObjectType("Query")]
public sealed class AdminSupplierQueries
{
    /// <summary>Suppliers with optional status filter (Pending/Verified/Suspended).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<SupplierSnapshot>> Suppliers(
        string? status, int page, int pageSize,
        [Service] ISupplierReadDataProvider suppliers, CancellationToken ct)
        => (await suppliers.ListSuppliersAsync(status, page, pageSize, ct)).ThrowIfFailure();

    /// <summary>Brand-supply agreements owned by one supplier.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<BrandAgreementRowDto>> SupplierAgreements(
        Guid supplierId, [Service] ISupplierReadDataProvider suppliers, CancellationToken ct)
        => (await suppliers.GetAgreementsBySupplierAsync(supplierId, ct)).ThrowIfFailure();
}

[ExtendObjectType("Query")]
public sealed class AdminBrandQueries
{
    /// <summary>Brands with optional status filter (PendingReview/Approved/Rejected).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<IReadOnlyList<BrandSnapshot>> Brands(
        string? status, int page, int pageSize,
        [Service] IBrandReadDataProvider brands, CancellationToken ct)
        => (await brands.ListBrandsAsync(status, page, pageSize, ct)).ThrowIfFailure();
}
