using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Supplier;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer;
using SupplierAggregate = ModularMonolith.Modules.Supplier.Application.Domain.Suppliers.Supplier;

namespace ModularMonolith.Modules.Supplier.QueryApplication;

public interface ISupplierQueryService
{
    Task<Result<SupplierSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierSnapshot>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Flat agreement rows for one supplier (Admin composes brand names).</summary>
    Task<IReadOnlyList<BrandAgreementRowDto>> GetAgreementsBySupplierAsync(Guid supplierId, CancellationToken ct = default);
}

/// <summary>Strictly read-only: direct EF Core projections (ADR-0008).</summary>
internal sealed class SupplierQueryService(SupplierDbContext db) : ISupplierQueryService
{
    public async Task<Result<SupplierSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<SupplierAggregate>()
            .Where(s => s.Id == id)
            .Select(s => new SupplierSnapshot(
                s.Id, s.Name, s.ContactEmail.Value, s.PhoneNumber, s.Address, s.Status.Value, s.IsVerified))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<SupplierSnapshot>(new Error("SUPPLIER_NOT_FOUND", "Supplier was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<SupplierSnapshot>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Set<SupplierAggregate>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status.Value == status);

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SupplierSnapshot(
                s.Id, s.Name, s.ContactEmail.Value, s.PhoneNumber, s.Address, s.Status.Value, s.IsVerified))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BrandAgreementRowDto>> GetAgreementsBySupplierAsync(Guid supplierId, CancellationToken ct = default)
        => await db.Set<SupplierAggregate>()
            .Where(s => s.Id == supplierId)
            .SelectMany(s => s.Agreements)
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new BrandAgreementRowDto(a.BrandId, a.CommissionRate, a.AssignedAt, a.IsActive))
            .ToListAsync(ct);
}
