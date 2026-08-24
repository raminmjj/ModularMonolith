using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` can build a RELATIONAL model
/// even though Development runs on the InMemory provider. Never used at runtime.
/// </summary>
public sealed class PaymentDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ModularMonolith_DesignTime_Payment;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options);
}
