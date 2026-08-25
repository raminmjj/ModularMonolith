using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories;

internal sealed class SupplierRepository : EfCoreRepository<Application.Domain.Suppliers.Supplier, Guid>,
    Application.Ports.Outbound.ISupplierRepository
{
    private readonly SupplierDbContext _db;
    public SupplierRepository(SupplierDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Suppliers.Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Suppliers.Include(s => s.Agreements).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Application.Domain.Suppliers.Supplier aggregate, CancellationToken ct = default)
        => await _db.Suppliers.AddAsync(aggregate, ct);
}

internal sealed class SupplierUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<SupplierDbContext> _inner;
    public SupplierUnitOfWork(SupplierDbContext db) => _inner = new(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _inner.SaveChangesAsync(ct);
}
