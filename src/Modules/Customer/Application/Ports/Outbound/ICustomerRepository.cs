namespace ModularMonolith.Modules.Customer.Application.Ports.Outbound;

/// <summary>
/// Outbound port — repository interface the hexagon needs from driven adapters.
/// NO EF Core types here — pure domain.
/// </summary>
public interface ICustomerRepository
{
    Task<Domain.Customers.Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsForIdentityUserAsync(Guid identityUserId, CancellationToken ct = default);
    Task AddAsync(Domain.Customers.Customer aggregate, CancellationToken ct = default);
}

/// <summary>Outbound port — unit of work for transactional persistence.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(
        Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}
