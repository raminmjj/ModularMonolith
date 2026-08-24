namespace ModularMonolith.Contracts.Customer;

/// <summary>
/// Cross-module view of account standing. Consumed by the Payment module via its
/// transactional ACL gateway — NOT an integration event.
/// </summary>
public sealed record CustomerStatusDto(Guid CustomerId, bool IsSuspended, string AccountTier);

/// <summary>
/// Saved payment instrument as an opaque VAULT TOKEN. Raw card numbers never
/// cross any module boundary (PCI: tokenization happens in the upstream vault).
/// </summary>
public sealed record SavedPaymentMethodDto(
    Guid PaymentMethodId,
    string TokenizedCardNumber,
    DateOnly ExpiryDate,
    string CardType);

/// <summary>Read-side projection (QueryApplication only).</summary>
public sealed record CustomerSnapshot(Guid Id, Guid IdentityUserId, string DisplayName, string Status, string AccountTier);
