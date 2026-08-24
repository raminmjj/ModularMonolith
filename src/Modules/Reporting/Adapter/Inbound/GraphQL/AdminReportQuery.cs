using HotChocolate.Authorization;
using HotChocolate.Execution;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Reporting.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Reporting.Adapter.Inbound.GraphQL;

/// <summary>
/// GraphQL query root for the admin panel. Driving adapter only: resolves the
/// inbound port, maps Result → GraphQL errors. All data comes from
/// IFailureReportService (which composes provider READ sides via ports) —
/// no DbContext, no aggregates, no write-side access.
/// </summary>
[Authorize(Policy = "Admin")]
public sealed class AdminReportQuery
{
    /// <summary>
    /// Customers with at least one failed payment, their failed payments and totals.
    /// Filters: date range (on FailedAt), minimum amount, customer status. Paged.
    /// </summary>
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
