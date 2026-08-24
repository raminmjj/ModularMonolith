using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

/// <summary>
/// Outbound port over the Payment READ side. Implemented in Adapter/Outbound by
/// delegating to Payment.QueryApplication — never to a write-side Application.
/// </summary>
public interface IPaymentReadDataProvider
{
    Task<Result<IReadOnlyList<FailedPaymentDto>>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default);
}

/// <summary>Outbound port over the Customer READ side (batch fetch, single SQL).</summary>
public interface ICustomerReadDataProvider
{
    Task<Result<IReadOnlyList<CustomerSnapshot>>> GetCustomersByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
