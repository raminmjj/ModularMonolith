namespace ModularMonolith.Contracts.Payment;

/// <summary>Read-side projection (QueryApplication only). MethodToken is a vault token, never a PAN.</summary>
public sealed record PaymentSnapshot(
    Guid Id,
    Guid CustomerId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string MethodToken);
