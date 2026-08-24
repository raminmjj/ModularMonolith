using Microsoft.Extensions.Logging;
using ModularMonolith.DDD.Common;
using ModularMonolith.Framework.Results;
using ModularMonolith.Framework.Sagas;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;
using ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Payment.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Payment.Application.Service;

/// <summary>
/// Inbound port implementation. Payment initiation is a CrossModuleSaga
/// (docs/adr/0005): step 1 verifies customer standing, step 2 fetches the saved
/// card TOKEN, step 3 persists the transaction — all synchronous, in-process.
///
/// The ACL steps are READ-ONLY against Customer, so they need no compensations;
/// if any step fails the saga returns before anything was persisted.
/// </summary>
public sealed class PaymentService : Ports.Inbound.IPaymentService
{
    private readonly IPaymentTransactionRepository _transactions;
    private readonly ICustomerGatewayPort _customers; // consumer-owned port — NOT Customer's inbound type
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentTransactionRepository transactions,
        ICustomerGatewayPort customers,
        IUnitOfWork unitOfWork,
        ILogger<PaymentService> logger)
        => (_transactions, _customers, _unitOfWork, _logger) = (transactions, customers, unitOfWork, logger);

    public async Task<Result<Guid>> InitiateWithSavedCardAsync(
        Guid customerId, Guid orderId, decimal amount, string currency,
        Guid savedPaymentMethodId, CancellationToken ct = default)
    {
        CustomerStatusResult? status = null;
        SavedMethodResult? method = null;
        PaymentTransaction? transaction = null;

        var saga = new CrossModuleSaga(_logger, $"payment-initiate:saved-card:{customerId}")
            .Step("verify-customer-standing",
                async token =>
                {
                    var r = await _customers.GetCustomerStatusAsync(customerId, token);
                    if (!r.IsSuccess) return Result.Failure(r.Error);
                    status = new CustomerStatusResult(r.Value!.IsSuspended);
                    return r.Value.IsSuspended
                        ? Result.Failure(new Error("CUSTOMER_SUSPENDED", "Account is suspended; payment refused."))
                        : Result.Success();
                })
            .Step("fetch-saved-card-token",
                async token =>
                {
                    var m = await _customers.GetSavedPaymentMethodAsync(customerId, savedPaymentMethodId, token);
                    if (!m.IsSuccess) return Result.Failure(m.Error);
                    if (m.Value!.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow))
                        return Result.Failure(new Error("CARD_EXPIRED", "The selected card has expired."));
                    method = new SavedMethodResult(m.Value);
                    return Result.Success();
                })
            .Step("persist-transaction",
                async token =>
                {
                    transaction = PaymentTransaction.Initiate(
                        customerId, orderId, Money.Create(amount, currency),
                        PaymentMethodSnapshot.FromSaved(method!.Dto));
                    await _transactions.AddAsync(transaction, token);
                    await _unitOfWork.SaveChangesAsync(token); // Payment DB only
                    return Result.Success();
                });

        var sagaResult = await saga.ExecuteAsync(ct);
        if (sagaResult.IsFailure) return Result.Failure<Guid>(sagaResult.Error);
        return Result.Success(transaction!.Id);
    }

    public async Task<Result<Guid>> InitiateWithNewCardTokenAsync(
        Guid customerId, Guid orderId, decimal amount, string currency,
        string cardToken, string cardType, DateOnly expiry, CancellationToken ct = default)
    {
        bool suspended = false;
        PaymentTransaction? transaction = null;
        PaymentMethodSnapshot? snapshot = null;

        var saga = new CrossModuleSaga(_logger, $"payment-initiate:new-token:{customerId}")
            .Step("verify-customer-standing",
                async token =>
                {
                    var r = await _customers.GetCustomerStatusAsync(customerId, token);
                    if (!r.IsSuccess) return Result.Failure(r.Error);
                    suspended = r.Value!.IsSuspended;
                    return suspended
                        ? Result.Failure(new Error("CUSTOMER_SUSPENDED", "Account is suspended; payment refused."))
                        : Result.Success();
                })
            .Step("validate-card-token",
                _ =>
                {
                    try { snapshot = PaymentMethodSnapshot.FromNewCardToken(cardToken, cardType, expiry); }
                    catch (DomainException dex) { return Task.FromResult(Result.Failure(new Error(dex.Code, dex.Message))); }
                    return Task.FromResult(Result.Success());
                })
            .Step("persist-transaction",
                async token =>
                {
                    transaction = PaymentTransaction.Initiate(
                        customerId, orderId, Money.Create(amount, currency), snapshot!);
                    await _transactions.AddAsync(transaction, token);
                    await _unitOfWork.SaveChangesAsync(token);
                    return Result.Success();
                });

        var sagaResult = await saga.ExecuteAsync(ct);
        if (sagaResult.IsFailure) return Result.Failure<Guid>(sagaResult.Error);
        return Result.Success(transaction!.Id);
    }

    public async Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
    {
        var tx = await _transactions.GetByIdAsync(paymentId, ct);
        if (tx is null) return Result.Failure(new Error("PAYMENT_NOT_FOUND", "Payment was not found."));
        try { tx.MarkRefunded(); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
    public async Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
    {
        var tx = await _transactions.GetByIdAsync(paymentId, ct);
        if (tx is null) return Result.Failure(new Error("PAYMENT_NOT_FOUND", "Payment was not found."));

        try { tx.MarkCaptured(); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
    {
        var tx = await _transactions.GetByIdAsync(paymentId, ct);
        if (tx is null) return Result.Failure(new Error("PAYMENT_NOT_FOUND", "Payment was not found."));

        try { tx.MarkFailed(reason); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private sealed record CustomerStatusResult(bool IsSuspended);
    private sealed record SavedMethodResult(Contracts.Customer.SavedPaymentMethodDto Dto);
}
