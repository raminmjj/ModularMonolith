using ModularMonolith.Contracts.Supplier;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Supplier.Application.Ports.Inbound;

/// <summary>Public inbound port of the Supplier hexagon.</summary>
public interface ISupplierService
{
    Task<Result<Guid>> CreateAsync(string name, string contactEmail, string? phoneNumber, string? address, CancellationToken ct = default);
    Task<Result<SupplierSnapshot>> GetAsync(Guid supplierId, CancellationToken ct = default);
}
