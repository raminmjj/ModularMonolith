using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer;

public sealed class SupplierDbContext(DbContextOptions<SupplierDbContext> options) : DbContext(options)
{
    public DbSet<Application.Domain.Suppliers.Supplier> Suppliers => Set<Application.Domain.Suppliers.Supplier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupplierDbContext).Assembly);

        // Aggregates configure RowVersion with IsRowVersion() (SQL Server optimistic
        // locking). The InMemory provider does not generate rowversion values —
        // disable the token there (see ModuleDbContextBase rationale).
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
