using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Payment.Application.Ports.Inbound;

public interface IPaymentService
{
    Task<Result<Guid>> InitiateWithSavedCardAsync(
        Guid customerId, Guid orderId, decimal amount, string currency,
        Guid savedPaymentMethodId, CancellationToken ct = default);

    Task<Result<Guid>> InitiateWithNewCardTokenAsync(
        Guid customerId, Guid orderId, decimal amount, string currency,
        string cardToken, string cardType, DateOnly expiry, CancellationToken ct = default);

    Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
    Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default);
}
