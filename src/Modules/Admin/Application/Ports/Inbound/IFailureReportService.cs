using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Admin.Application.Ports.Inbound;

/// <summary>Inbound port consumed by the GraphQL adapter.</summary>
public interface IFailureReportService
{
    Task<Result<FailureReportPage>> GetCustomersWithFailedPaymentsAsync(FailureReportFilter filter, CancellationToken ct = default);
}
