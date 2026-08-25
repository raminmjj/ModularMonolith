using ModularMonolith.Contracts.Brand;
using ModularMonolith.Contracts.Supplier;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Application.Ports.Inbound;
using ModularMonolith.Modules.Supplier.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Outbound;

// ─── Supplier & Brand ACL adapters (ADR-0009) ───
// Reads: delegate to provider QueryApplications (flat rows).
// Writes: delegate to the OWNING module's dedicated admin port — the capability
// lives in the domain; Admin only crosses the boundary.

internal sealed class SuppliersAdminGateway(Supplier.Application.Ports.Inbound.ISupplierAdminService suppliers)
    : ISupplierAdminGateway
{
    public Task<Result> VerifyAsync(Guid supplierId, CancellationToken ct = default)
        => suppliers.VerifyAsync(supplierId, ct);

    public Task<Result> SuspendAsync(Guid supplierId, CancellationToken ct = default)
        => suppliers.SuspendAsync(supplierId, ct);

    public Task<Result> UpdateAsync(Guid supplierId, string name, string? phoneNumber, string? address,
        CancellationToken ct = default)
        => suppliers.UpdateAsync(supplierId, name, phoneNumber, address, ct);

    public Task<Result> AssignBrandAsync(Guid supplierId, Guid brandId, decimal commissionRate,
        CancellationToken ct = default)
        => suppliers.AssignBrandAsync(supplierId, brandId, commissionRate, ct);

    public Task<Result> RemoveBrandAsync(Guid supplierId, Guid brandId, CancellationToken ct = default)
        => suppliers.RemoveBrandAsync(supplierId, brandId, ct);
}

internal sealed class BrandsAdminGateway(Brand.Application.Ports.Inbound.IBrandAdminService brands)
    : IBrandAdminGateway
{
    public async Task<Result<Guid>> CreateBrandAsync(string name, string? slug, string? description,
        string? logoUrl, string countryOfOrigin, CancellationToken ct = default)
    {
        var result = await brands.CreateAsync(name, slug, description, logoUrl, countryOfOrigin, ct);
        return result.IsSuccess ? Result.Success(result.Value!) : Result.Failure<Guid>(result.Error);
    }

    public Task<Result> ApproveBrandAsync(Guid brandId, CancellationToken ct = default)
        => brands.ApproveAsync(brandId, ct);

    public Task<Result> RejectBrandAsync(Guid brandId, string reason, CancellationToken ct = default)
        => brands.RejectAsync(brandId, reason, ct);

    public Task<Result> UpdateBrandDetailsAsync(Guid brandId, string name, string? description,
        string? logoUrl, string countryOfOrigin, CancellationToken ct = default)
        => brands.UpdateDetailsAsync(brandId, name, description, logoUrl, countryOfOrigin, ct);
}

/// <summary>Supplier read rows via Supplier.QueryApplication.</summary>
public sealed class SuppliersReadDataProvider(Supplier.QueryApplication.ISupplierQueryService suppliers)
    : ISupplierReadDataProvider
{
    public async Task<Result<IReadOnlyList<SupplierSnapshot>>> ListSuppliersAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await suppliers.ListAsync(status, page, pageSize, ct));

    public async Task<Result<IReadOnlyList<BrandAgreementRowDto>>> GetAgreementsBySupplierAsync(
        Guid supplierId, CancellationToken ct = default)
        => Result.Success(await suppliers.GetAgreementsBySupplierAsync(supplierId, ct));
}

/// <summary>Brand read rows via Brand.QueryApplication.</summary>
public sealed class BrandsReadDataProvider(Brand.QueryApplication.IBrandQueryService brands)
    : IBrandReadDataProvider
{
    public async Task<Result<IReadOnlyList<BrandSnapshot>>> ListBrandsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
        => Result.Success(await brands.ListAsync(status, page, pageSize, ct));
}
