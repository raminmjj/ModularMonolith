using HotChocolate.Authorization;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;

// ─── Supplier & Brand admin mutations (ADR-0009) ───
// Thin shells over the gateway ports; the domain modules own the operations.
// Brand existence for AssignBrandToSupplier is enforced inside the Supplier module
// via its ACL — a failed check surfaces here as BRAND_NOT_FOUND.

[ExtendObjectType("Mutation")]
public sealed class AdminSupplierMutations
{
    /// <summary>Verify a pending supplier.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> VerifySupplier(
        Guid supplierId, [Service] ISupplierAdminGateway gateway, CancellationToken ct)
    { (await gateway.VerifyAsync(supplierId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Suspend a verified supplier (revokes verification).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> SuspendSupplier(
        Guid supplierId, [Service] ISupplierAdminGateway gateway, CancellationToken ct)
    { (await gateway.SuspendAsync(supplierId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Update supplier profile (name, phone, address).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> UpdateSupplier(
        Guid supplierId, string name, string? phoneNumber, string? address,
        [Service] ISupplierAdminGateway gateway, CancellationToken ct)
    { (await gateway.UpdateAsync(supplierId, name, phoneNumber, address, ct)).ThrowIfFailure(); return true; }

    /// <summary>Assign a brand to a verified supplier with a commission rate.
    /// Fails with SUPPLIER_NOT_VERIFIED / BRAND_ALREADY_SUPPLIED / BRAND_NOT_FOUND.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> AssignBrandToSupplier(
        Guid supplierId, Guid brandId, decimal commissionRate,
        [Service] ISupplierAdminGateway gateway, CancellationToken ct)
    { (await gateway.AssignBrandAsync(supplierId, brandId, commissionRate, ct)).ThrowIfFailure(); return true; }

    /// <summary>Remove (deactivate) a supplier's agreement with a brand.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> RemoveBrandFromSupplier(
        Guid supplierId, Guid brandId, [Service] ISupplierAdminGateway gateway, CancellationToken ct)
    { (await gateway.RemoveBrandAsync(supplierId, brandId, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminBrandMutations
{
    /// <summary>Create a brand (starts in PendingReview).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<Guid> CreateBrand(
        string name, string? slug, string? description, string? logoUrl, string countryOfOrigin,
        [Service] IBrandAdminGateway gateway, CancellationToken ct)
        => (await gateway.CreateBrandAsync(name, slug, description, logoUrl, countryOfOrigin, ct)).ThrowIfFailure();

    /// <summary>Approve a pending brand.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ApproveBrand(
        Guid brandId, [Service] IBrandAdminGateway gateway, CancellationToken ct)
    { (await gateway.ApproveBrandAsync(brandId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Reject a pending brand with a mandatory reason.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> RejectBrand(
        Guid brandId, string reason, [Service] IBrandAdminGateway gateway, CancellationToken ct)
    { (await gateway.RejectBrandAsync(brandId, reason, ct)).ThrowIfFailure(); return true; }

    /// <summary>Update brand details (name, description, logo, country).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> UpdateBrandDetails(
        Guid brandId, string name, string? description, string? logoUrl, string countryOfOrigin,
        [Service] IBrandAdminGateway gateway, CancellationToken ct)
    { (await gateway.UpdateBrandDetailsAsync(brandId, name, description, logoUrl, countryOfOrigin, ct)).ThrowIfFailure(); return true; }
}
