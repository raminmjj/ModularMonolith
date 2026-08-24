using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build a RELATIONAL model
/// even though Development normally runs on the InMemory provider (which cannot
/// produce migrations). The connection string below is never used at runtime.
/// </summary>
public sealed class OrdersDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Orders;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
