using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;
using CustomerAggregate = ModularMonolith.Modules.Customer.Application.Domain.Customers.Customer;

using ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Customer.QueryApplication;

public interface ICustomerQueryService
{
    Task<Result<CustomerSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Batch fetch for cross-module report composition (single SQL IN query).</summary>
    Task<IReadOnlyList<CustomerSnapshot>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

/// <summary>
/// Strictly read-only: direct EF Core projection into a Contract record.
/// No aggregate behavior, no SaveChanges (arch-tested: no outbound port references).
/// </summary>
internal sealed class CustomerQueryService(CustomerDbContext db) : ICustomerQueryService
{
    public async Task<Result<CustomerSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<CustomerAggregate>()
            .Where(c => c.Id == id)
            .Select(c => new CustomerSnapshot(c.Id, c.IdentityUserId, c.DisplayName, c.Status.Value, c.AccountTier))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<CustomerSnapshot>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<CustomerSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default)
        => await db.Set<CustomerAggregate>()
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CustomerSnapshot(c.Id, c.IdentityUserId, c.DisplayName, c.Status.Value, c.AccountTier))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CustomerSnapshot>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        return await db.Set<CustomerAggregate>()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new CustomerSnapshot(c.Id, c.IdentityUserId, c.DisplayName, c.Status.Value, c.AccountTier))
            .ToListAsync(ct);
    }
}
