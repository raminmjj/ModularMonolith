using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer;

/// <summary>Design-time factory for relational migrations (dev runs InMemory).</summary>
public sealed class SupplierDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SupplierDbContext>
{
    public SupplierDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SupplierDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Supplier;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
