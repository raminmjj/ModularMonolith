using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;

/// <summary>
/// Immutable snapshot of the instrument used for a transaction. Holds a VAULT
/// TOKEN only — the factory rejects anything that is not a 'tok_…' value, so raw
/// card data can never enter the Payment module.
/// </summary>
public sealed class PaymentMethodSnapshot : ValueObject
{
    public string Token { get; }
    public string CardType { get; }
    public DateOnly ExpiryDate { get; }

    private PaymentMethodSnapshot(string token, string cardType, DateOnly expiryDate)
        => (Token, CardType, ExpiryDate) = (token, cardType, expiryDate);

    public static PaymentMethodSnapshot FromSaved(Contracts.Customer.SavedPaymentMethodDto dto) =>
        new(NormalizeToken(dto.TokenizedCardNumber), dto.CardType, dto.ExpiryDate);

    public static PaymentMethodSnapshot FromNewCardToken(string token, string cardType, DateOnly expiry) =>
        new(NormalizeToken(token), cardType, expiry);

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("tok_"))
            throw new DomainException("PAYMENT_TOKEN_INVALID",
                "Only vault tokens are accepted. Raw card data must never reach the Payment module.");
        return token;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Token;
        yield return CardType;
        yield return ExpiryDate;
    }
}
