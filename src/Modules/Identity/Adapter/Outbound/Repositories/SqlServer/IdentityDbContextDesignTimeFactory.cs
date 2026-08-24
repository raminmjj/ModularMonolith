using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build a RELATIONAL model
/// even though Development normally runs on the InMemory provider (which cannot
/// produce migrations). The connection string below is never used at runtime.
/// </summary>
public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Identity;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
