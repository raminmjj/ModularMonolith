namespace ModularMonolith.Modules.Orders.Application.Ports.Outbound;

public interface IOrderRepository
{
    Task<Domain.Orders.Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Domain.Orders.Order>> ListForUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Domain.Orders.Order aggregate, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}
