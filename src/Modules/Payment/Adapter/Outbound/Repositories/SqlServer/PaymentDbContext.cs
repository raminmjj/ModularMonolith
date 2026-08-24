using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;

public sealed class PaymentDbContext : ModuleDbContextBase
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options, "payment") { }

    public DbSet<Application.Domain.Payments.PaymentTransaction> Transactions => Set<Application.Domain.Payments.PaymentTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
