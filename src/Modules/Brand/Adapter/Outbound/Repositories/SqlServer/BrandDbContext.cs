using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer;

public sealed class BrandDbContext(DbContextOptions<BrandDbContext> options) : DbContext(options)
{
    public DbSet<Application.Domain.Brands.Brand> Brands => Set<Application.Domain.Brands.Brand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrandDbContext).Assembly);

        // InMemory provider cannot generate rowversion values — disable the token
        // there (see ModuleDbContextBase rationale / AGENTS.md pitfalls).
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties().Where(p => p.Name == "RowVersion"))
                {
                    property.IsConcurrencyToken = false;
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }
        }

        base.OnModelCreating(modelBuilder);
    }
}
