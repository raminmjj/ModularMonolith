using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer;

/// <summary>Design-time factory for relational migrations (dev runs InMemory).</summary>
public sealed class BrandDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BrandDbContext>
{
    public BrandDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BrandDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Brand;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
