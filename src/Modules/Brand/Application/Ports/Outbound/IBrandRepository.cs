namespace ModularMonolith.Modules.Brand.Application.Ports.Outbound;

public interface IBrandRepository
{
    Task<Domain.Brands.Brand?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Domain.Brands.Brand aggregate, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
