using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Supplier.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Supplier.Application.Service;

/// <summary>
/// Admin operations for the Supplier context (ADR-0007 amendment: domain-owned).
/// Every mutation publishes the aggregate&apos;s raised events via the pluggable
/// dispatcher (NoOp today — ADR-0004) after a successful save.
/// </summary>
public sealed class SupplierAdminService(
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork,
    IBrandExistenceChecker brandChecker,
    IEventDispatcher eventDispatcher) : Ports.Inbound.ISupplierAdminService
{
    public async Task<Result> VerifyAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await RequireSupplierAsync(supplierId, ct);
        if (!supplier.IsSuccess) return supplier;

        try { supplier.Value!.Verify(); }
        catch (DDD.Common.DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier.Value!, ct);
        return Result.Success();
    }

    public async Task<Result> SuspendAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await RequireSupplierAsync(supplierId, ct);
        if (!supplier.IsSuccess) return supplier;

        try { supplier.Value!.Suspend(); }
        catch (DDD.Common.DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier.Value!, ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(Guid supplierId, string name, string? phoneNumber, string? address,
        CancellationToken ct = default)
    {
        var supplier = await RequireSupplierAsync(supplierId, ct);
        if (!supplier.IsSuccess) return supplier;

        try { supplier.Value!.UpdateProfile(name, phoneNumber, address); }
        catch (DDD.Common.DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier.Value!, ct);
        return Result.Success();
    }

    /// <summary>
    /// Cross-module guard first (Brand must exist — ACL), then aggregate invariants,
    /// save, and publish BrandAgreementAssignedDomainEvent through the seam.
    /// </summary>
    public async Task<Result> AssignBrandAsync(Guid supplierId, Guid brandId, decimal commissionRate,
        CancellationToken ct = default)
    {
        var brandCheck = await brandChecker.EnsureBrandExistsAsync(brandId, ct);
        if (!brandCheck.IsSuccess) return brandCheck;

        var supplier = await RequireSupplierAsync(supplierId, ct);
        if (!supplier.IsSuccess) return supplier;

        try { supplier.Value!.AddBrandAgreement(brandId, commissionRate, DateTimeOffset.UtcNow); }
        catch (DDD.Common.DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier.Value!, ct);
        return Result.Success();
    }

    public async Task<Result> RemoveBrandAsync(Guid supplierId, Guid brandId, CancellationToken ct = default)
    {
        var supplier = await RequireSupplierAsync(supplierId, ct);
        if (!supplier.IsSuccess) return supplier;

        try { supplier.Value!.RemoveBrandAgreement(brandId); }
        catch (DDD.Common.DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier.Value!, ct);
        return Result.Success();
    }

    private async Task<Result<Domain.Suppliers.Supplier>> RequireSupplierAsync(Guid id, CancellationToken ct)
    {
        var s = await suppliers.GetByIdAsync(id, ct);
        return s is null
            ? Result.Failure<Domain.Suppliers.Supplier>(new Error("SUPPLIER_NOT_FOUND", "Supplier was not found."))
            : Result.Success(s);
    }
}
