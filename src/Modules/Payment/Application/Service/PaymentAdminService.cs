using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Payment.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Payment.Application.Service;

/// <summary>Admin facade over the Payment public service — delegation is deliberate (ADR-0007).</summary>
public sealed class PaymentAdminService(IPaymentService payments) : IPaymentAdminService
{
    public Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
        => payments.CaptureAsync(paymentId, ct);

    public Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
        => payments.FailAsync(paymentId, reason, ct);

    public Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
        => payments.RefundAsync(paymentId, ct);
}
