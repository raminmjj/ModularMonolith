namespace ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Dtos;

/// <summary>Request bodies carry ONLY vault tokens — no field anywhere accepts a PAN.</summary>
public sealed record InitiateSavedCardPaymentRequest(
    Guid CustomerId, Guid OrderId, decimal Amount, string Currency, Guid SavedPaymentMethodId);

public sealed record InitiateNewCardTokenPaymentRequest(
    Guid CustomerId, Guid OrderId, decimal Amount, string Currency,
    string CardToken, string CardType, DateOnly ExpiryDate);

public sealed record FailPaymentRequest(string Reason);

public sealed record PaymentCreatedResponse(Guid Id, string Status);
