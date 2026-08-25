using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories;

internal sealed class BrandRepository : EfCoreRepository<Application.Domain.Brands.Brand, Guid>,
    Application.Ports.Outbound.IBrandRepository
{
    private readonly BrandDbContext _db;
    public BrandRepository(BrandDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Brands.Brand?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Brands.AnyAsync(b => b.Slug.Value == slug, ct);

    public async Task AddAsync(Application.Domain.Brands.Brand aggregate, CancellationToken ct = default)
        => await _db.Brands.AddAsync(aggregate, ct);
}

internal sealed class BrandUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<BrandDbContext> _inner;
    public BrandUnitOfWork(BrandDbContext db) => _inner = new(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _inner.SaveChangesAsync(ct);
}
