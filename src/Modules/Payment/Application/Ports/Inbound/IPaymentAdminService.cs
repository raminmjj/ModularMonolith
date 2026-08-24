using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Payment.Application.Ports.Inbound;

/// <summary>Admin-owned operations for the Payment context (ADR-0007 amendment).</summary>
public interface IPaymentAdminService
{
    Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
    Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default);
}
