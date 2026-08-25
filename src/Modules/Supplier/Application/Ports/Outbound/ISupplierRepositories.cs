using ModularMonolith.Framework.Results;
namespace ModularMonolith.Modules.Supplier.Application.Ports.Outbound;

/// <summary>Outbound port — persistence for the Supplier aggregate.</summary>
public interface ISupplierRepository
{
    Task<Domain.Suppliers.Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Domain.Suppliers.Supplier aggregate, CancellationToken ct = default);
}

/// <summary>Outbound port — unit of work.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// ACL port — verifies Brand existence before Supplier mutates an agreement.
/// Implemented in Adapter/Outbound by delegating to Brand's inbound port
/// (sanctioned exception #5: Supplier.Adapter.Outbound → Brand.Application).
/// </summary>
public interface IBrandExistenceChecker
{
    Task<Result> EnsureBrandExistsAsync(Guid brandId, CancellationToken ct = default);
}
