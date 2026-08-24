using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Payment.Application.Domain.Events;
using ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Payment.Application.Domain.Payments;

public sealed class PaymentTransaction : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid OrderId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentStatus Status { get; private set; } = null!;
    public PaymentMethodSnapshot Method { get; private set; } = null!; // token snapshot, never a PAN
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }

    private PaymentTransaction() { }

    /// <summary>Caller must verify customer standing BEFORE calling this factory (saga step 1).</summary>
    public static PaymentTransaction Initiate(Guid customerId, Guid orderId, Money amount, PaymentMethodSnapshot method)
    {
        if (customerId == Guid.Empty || orderId == Guid.Empty)
            throw new DomainException("PARTY_IDS_REQUIRED", "Customer and order ids are required.");
        if (amount.Amount <= 0)
            throw new DomainException("AMOUNT_POSITIVE_REQUIRED", "Amount must be positive.");

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderId = orderId,
            Amount = amount,
            Method = method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        tx.Raise(new PaymentInitiatedDomainEvent(tx.Id, customerId, amount.Amount, amount.Currency));
        return tx;
    }

    public void MarkCaptured()
    {
        EnsurePending();
        Status = PaymentStatus.Captured;
        Raise(new PaymentCapturedDomainEvent(Id));
    }

    public void MarkFailed(string reason)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("FAILURE_REASON_REQUIRED", "A failure reason is required.");
        Status = PaymentStatus.Failed;
        FailedAt = DateTimeOffset.UtcNow;
        FailureReason = reason.Trim();
        Raise(new PaymentFailedDomainEvent(Id, reason));
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("PAYMENT_NOT_PENDING", $"Cannot transition from '{Status.Value}'.");
    }
}

public sealed class PaymentStatus : ValueObject
{
    public static readonly PaymentStatus Pending = new("Pending");
    public static readonly PaymentStatus Captured = new("Captured");
    public static readonly PaymentStatus Failed = new("Failed");

    public string Value { get; }
    private PaymentStatus(string value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public static PaymentStatus Parse(string value) => value switch
    {
        "Pending" => Pending,
        "Captured" => Captured,
        "Failed" => Failed,
        _ => throw new DomainException("PAYMENT_STATUS_INVALID", $"'{value}' is not a valid payment status."),
    };
}
