using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Customer.Application.Domain.Customers;

/// <summary>
/// Saved payment instrument — entity within the Customer aggregate (no independent
/// lifecycle). Stores a VAULT TOKEN only; the domain itself rejects raw PANs.
/// </summary>
public sealed class SavedPaymentMethod : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public string TokenizedCard { get; private set; } = null!;
    public string CardType { get; private set; } = null!;
    public DateOnly ExpiryDate { get; private set; }
    public bool IsDefault { get; private set; }

    private SavedPaymentMethod() { }

    internal static SavedPaymentMethod Create(Guid customerId, string token, string cardType, DateOnly expiry, int index)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("tok_"))
            throw new DomainException("PAYMENT_METHOD_TOKEN_INVALID",
                "A vault token ('tok_…') is required. Raw card numbers are not accepted.");
        if (string.IsNullOrWhiteSpace(cardType))
            throw new DomainException("PAYMENT_METHOD_CARDTYPE_REQUIRED", "Card type is required.");

        return new SavedPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TokenizedCard = token,
            CardType = cardType.Trim(),
            ExpiryDate = expiry,
            IsDefault = index == 0,
        };
    }
}
