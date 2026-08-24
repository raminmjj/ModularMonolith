using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build a RELATIONAL model
/// even though Development normally runs on the InMemory provider (which cannot
/// produce migrations). The connection string below is never used at runtime.
/// </summary>
public sealed class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Catalog;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
