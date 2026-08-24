using HotChocolate.Authorization;
using HotChocolate.Execution;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Reporting.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Reporting.Adapter.Inbound.GraphQL;

/// <summary>
/// Failed-payments report (ADR-0006). Composes Customer/Payment/Orders READ sides
/// through IFailureReportService — resolvers stay IO-free shells.
/// </summary>
[ExtendObjectType("Query")]
public sealed class AdminReportQueries
{
    /// <summary>
    /// Customers with at least one failed payment, their failed payments, totals
    /// and last order. Filters: date range (on FailedAt), minimum amount, customer status.
    /// </summary>
    [Authorize(Policy = "Admin")]
    public async Task<FailureReportPage> CustomersWithFailedPayments(
        FailureReportFilterInput filter,
        [Service] IFailureReportService reportService,
        CancellationToken cancellationToken)
    {
        var result = await reportService.GetCustomersWithFailedPaymentsAsync(filter.ToDto(), cancellationToken);
        if (result.IsSuccess) return result.Value!;

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(result.Error!.Message)
                .SetCode(result.Error.Code)
                .Build());
    }
}

/// <summary>GraphQL input type — kept separate from the domain contract record.</summary>
public sealed record FailureReportFilterInput(
    DateTimeOffset? From,
    DateTimeOffset? To,
    decimal? MinAmount,
    string? CustomerStatus,
    int Page = 1,
    int PageSize = 20)
{
    public FailureReportFilter ToDto() => new(From, To, MinAmount, CustomerStatus, Page, PageSize);
}
