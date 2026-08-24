using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Payment.Application.Ports.Inbound;
using ModularMonolith.Modules.Payment.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Payment.Application.Service;

/// <summary>
/// Admin facade over the Payment context (ADR-0007). Capture/Fail delegate to the
/// public service; RefundAsync is MATERIALIZED to publish the aggregate's
/// <see cref="PaymentRefundedDomainEvent"/> through the pluggable dispatcher seam
/// (NoOp today — ADR-0004). Same domain rules: only Captured payments refund.
/// </summary>
public sealed class PaymentAdminService(
    IPaymentService payments,
    IPaymentTransactionRepository transactions,
    IUnitOfWork unitOfWork,
    IEventDispatcher eventDispatcher) : IPaymentAdminService
{
    public Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
        => payments.CaptureAsync(paymentId, ct);

    public Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
        => payments.FailAsync(paymentId, reason, ct);

    public async Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
    {
        var tx = await transactions.GetByIdAsync(paymentId, ct);
        if (tx is null)
            return Result.Failure(new Error("PAYMENT_NOT_FOUND", "Payment was not found."));

        try { tx.MarkRefunded(); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);
        await eventDispatcher.DispatchAndClearAsync(tx, ct); // NoOp today — ADR-0004
        return Result.Success();
    }
}
