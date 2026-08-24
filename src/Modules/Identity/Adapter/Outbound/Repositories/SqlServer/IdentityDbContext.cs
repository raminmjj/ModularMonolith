using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;

public sealed class IdentityDbContext : ModuleDbContextBase
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options, "identity") { }
    public DbSet<Application.Domain.Users.User> Users => Set<Application.Domain.Users.User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
