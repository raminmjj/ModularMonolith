namespace ModularMonolith.Modules.Payment.Application.Ports.Outbound;

public interface IPaymentTransactionRepository
{
    Task<Domain.Payments.PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Domain.Payments.PaymentTransaction aggregate, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(
        Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}
