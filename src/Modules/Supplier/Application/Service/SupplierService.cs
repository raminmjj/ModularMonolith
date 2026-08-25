using ModularMonolith.Contracts.Supplier;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Supplier.Application.Domain.Suppliers;
using ModularMonolith.Modules.Supplier.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Supplier.Application.Service;

/// <summary>Public inbound port implementation.</summary>
public sealed class SupplierService(
    ISupplierRepository suppliers,
    IUnitOfWork unitOfWork,
    IEventDispatcher eventDispatcher) : Ports.Inbound.ISupplierService
{
    public async Task<Result<Guid>> CreateAsync(string name, string contactEmail, string? phoneNumber,
        string? address, CancellationToken ct = default)
    {
        var supplier = Domain.Suppliers.Supplier.Create(name, contactEmail, phoneNumber, address);

        await suppliers.AddAsync(supplier, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(supplier, ct); // NoOp today (ADR-0004)
        return Result.Success(supplier.Id);
    }

    public async Task<Result<SupplierSnapshot>> GetAsync(Guid supplierId, CancellationToken ct = default)
    {
        var s = await suppliers.GetByIdAsync(supplierId, ct);
        return s is null
            ? Result.Failure<SupplierSnapshot>(new Error("SUPPLIER_NOT_FOUND", "Supplier was not found."))
            : Result.Success(new SupplierSnapshot(
                s.Id, s.Name, s.ContactEmail.Value, s.PhoneNumber, s.Address, s.Status.Value, s.IsVerified));
    }
}
