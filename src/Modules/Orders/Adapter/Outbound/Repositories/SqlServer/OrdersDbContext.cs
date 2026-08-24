using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;

public sealed class OrdersDbContext : ModuleDbContextBase
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options, "orders") { }
    public DbSet<Application.Domain.Orders.Order> Orders => Set<Application.Domain.Orders.Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
