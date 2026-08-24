using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.QueryApplication;
using ModularMonolith.Modules.Payment.QueryApplication;
using ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Reporting.Adapter.Outbound;

/// <summary>
/// READ-side ACL adapters. Pure delegation to provider QueryApplications —
/// flat Contract DTOs in, flat DTOs out. No write-side types are reachable here
/// (arch-tested: Reporting may never reference any module's Application assembly).
/// </summary>
internal sealed class PaymentReadDataProvider(IPaymentQueryService payments) : IPaymentReadDataProvider
{
    public async Task<Result<IReadOnlyList<FailedPaymentDto>>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default)
        => Result.Success(await payments.GetFailedPaymentsAsync(from, to, minAmount, ct));
}

internal sealed class CustomerReadDataProvider(ICustomerQueryService customers) : ICustomerReadDataProvider
{
    public async Task<Result<IReadOnlyList<CustomerSnapshot>>> GetCustomersByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Result.Success(await customers.GetByIdsAsync(ids, ct));
}
