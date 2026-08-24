using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Infrastructure.Persistence;

/// <summary>
/// Unit of Work abstraction for transactional operations. Scope: ONE module's
/// DbContext only. Cross-module operations do NOT share a transaction (separate
/// databases/connections, no TransactionScope) — they are synchronous sagas with
/// best-effort compensation via CrossModuleSaga. See docs/adr/0005.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, CancellationToken ct = default);
}

public sealed class EfCoreUnitOfWork<TDbContext> : IUnitOfWork where TDbContext : DbContext
{
    private readonly TDbContext _db;
    public EfCoreUnitOfWork(TDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> action, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action();
                if (result.IsSuccess) await tx.CommitAsync(ct);
                else await tx.RollbackAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}

public abstract class EfCoreRepository<TAggregate, TId>
    where TAggregate : class
    where TId : struct
{
    protected DbContext Context { get; }
    protected DbSet<TAggregate> Set { get; }

    protected EfCoreRepository(DbContext context)
    {
        Context = context;
        Set = context.Set<TAggregate>();
    }

    public virtual Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default)
        => Set.FindAsync(new object?[] { id }, ct).AsTask();

    public virtual async Task AddAsync(TAggregate aggregate, CancellationToken ct = default)
        => await Set.AddAsync(aggregate, ct);
}

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration config,
        string connectionStringName,
        string inMemoryDbName,
        string migrationsHistoryTable,
        bool isDevelopment)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(opts =>
        {
            var cs = config.GetConnectionString(connectionStringName);
            if (!string.IsNullOrWhiteSpace(cs))
                opts.UseSqlServer(cs, sql => sql.MigrationsHistoryTable(migrationsHistoryTable));
            else if (isDevelopment)
                opts.UseInMemoryDatabase(inMemoryDbName);
            else
                throw new InvalidOperationException(
                    $"Connection string '{connectionStringName}' is not configured and the environment is not Development. " +
                    "Refusing to fall back to the InMemory provider (silent data loss). Configure the connection string or run in Development.");
        });
        return services;
    }
}

public abstract class ModuleDbContextBase : DbContext
{
    private readonly string _schema;
    protected ModuleDbContextBase(DbContextOptions options, string schema) : base(options) => _schema = schema;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);

        // Aggregates configure RowVersion with IsRowVersion() (SQL Server
        // auto-generated optimistic locking). The InMemory provider does NOT
        // generate rowversion values, so keeping the token there makes EVERY
        // update throw DbUpdateConcurrencyException. Disable it for InMemory
        // (dev/test); real databases keep full optimistic concurrency.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties().Where(p => p.Name == "RowVersion"))
                {
                    property.IsConcurrencyToken = false;
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }
        }

        base.OnModelCreating(modelBuilder);
    }
}
