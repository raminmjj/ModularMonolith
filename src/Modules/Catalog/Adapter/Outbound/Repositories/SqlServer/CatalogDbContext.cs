using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;

public sealed class CatalogDbContext : ModuleDbContextBase
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options, "catalog") { }

    public DbSet<Application.Domain.Products.Product> Products => Set<Application.Domain.Products.Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
