using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Supplier.Application.Ports.Inbound;

/// <summary>
/// Admin-owned operations for the Supplier context (ADR-0007 amendment).
/// Assignment/removal publish aggregate events through the pluggable dispatcher
/// (ADR-0004) after a successful save.
/// </summary>
public interface ISupplierAdminService
{
    Task<Result> VerifyAsync(Guid supplierId, CancellationToken ct = default);
    Task<Result> SuspendAsync(Guid supplierId, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid supplierId, string name, string? phoneNumber, string? address, CancellationToken ct = default);
    Task<Result> AssignBrandAsync(Guid supplierId, Guid brandId, decimal commissionRate, CancellationToken ct = default);
    Task<Result> RemoveBrandAsync(Guid supplierId, Guid brandId, CancellationToken ct = default);
}
