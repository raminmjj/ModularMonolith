using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build a RELATIONAL model
/// even though Development runs on the InMemory provider. Never used at runtime.
/// </summary>
public sealed class CustomerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Customer;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
