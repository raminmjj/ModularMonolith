using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;

public sealed class CustomerDbContext : ModuleDbContextBase
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options, "customer") { }

    public DbSet<Application.Domain.Customers.Customer> Customers => Set<Application.Domain.Customers.Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
