namespace ModularMonolith.Modules.Catalog.Application.Ports.Outbound;

/// <summary>
/// Outbound port — repository interface that the hexagon needs from
/// outbound adapters (SQL DB, Redis, etc.). The Application defines WHAT it
/// needs; the adapter implements HOW. NO EF Core types here — pure domain.
/// </summary>
public interface IProductRepository
{
    Task<Domain.Products.Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Domain.Products.Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Domain.Products.Product>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Domain.Products.Product>> FindWithExpiredReservationsAsync(DateTimeOffset utcNow, CancellationToken ct = default);
    Task AddAsync(Domain.Products.Product aggregate, CancellationToken ct = default);
}

/// <summary>
/// Outbound port — unit of work for transactional persistence.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}
