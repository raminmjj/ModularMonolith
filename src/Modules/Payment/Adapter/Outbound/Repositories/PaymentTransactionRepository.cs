using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories;

/// <summary>Driven adapter — implements IPaymentTransactionRepository (outbound port) with EF Core.</summary>
internal sealed class PaymentTransactionRepository
    : EfCoreRepository<Application.Domain.Payments.PaymentTransaction, Guid>,
      Application.Ports.Outbound.IPaymentTransactionRepository
{
    private readonly PaymentDbContext _db;
    public PaymentTransactionRepository(PaymentDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Payments.PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(Application.Domain.Payments.PaymentTransaction aggregate, CancellationToken ct = default)
        => await _db.Transactions.AddAsync(aggregate, ct);
}

internal sealed class PaymentUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<PaymentDbContext> _inner;
    public PaymentUnitOfWork(PaymentDbContext db) => _inner = new EfCoreUnitOfWork<PaymentDbContext>(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default)
        => _inner.ExecuteInTransactionAsync(action, ct);
}
