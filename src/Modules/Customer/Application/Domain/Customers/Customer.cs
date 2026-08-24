using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Customer.Application.Domain.Events;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Customer.Application.Domain.Customers;

public sealed class Customer : AggregateRoot<Guid>
{
    public Guid IdentityUserId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public CustomerStatus Status { get; private set; } = null!;
    public string AccountTier { get; private set; } = "Standard";
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<Address> _addresses = [];
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    private readonly List<SavedPaymentMethod> _paymentMethods = [];
    public IReadOnlyCollection<SavedPaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    private Customer() { }

    public static Customer Register(Guid identityUserId, string displayName)
    {
        if (identityUserId == Guid.Empty)
            throw new DomainException("CUSTOMER_IDENTITY_REQUIRED", "Identity user id is required.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("CUSTOMER_NAME_REQUIRED", "Display name is required.");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            IdentityUserId = identityUserId,
            DisplayName = displayName.Trim(),
            Status = CustomerStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        customer.Raise(new CustomerRegisteredDomainEvent(customer.Id, customer.IdentityUserId));
        return customer;
    }

    public void AddAddress(Address address) => _addresses.Add(address);

    /// <summary>Stores a VAULT TOKEN — this method must never receive a raw PAN.</summary>
    public SavedPaymentMethod AddSavedPaymentMethod(string tokenizedCard, string cardType, DateOnly expiry)
    {
        var method = SavedPaymentMethod.Create(Id, tokenizedCard, cardType, expiry, index: _paymentMethods.Count);
        _paymentMethods.Add(method);
        Raise(new SavedPaymentMethodAddedDomainEvent(Id, method.Id));
        return method;
    }

    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended) return;
        Status = CustomerStatus.Suspended;
        Raise(new CustomerSuspendedDomainEvent(Id));
    }

    public void Reactivate()
    {
        if (Status == CustomerStatus.Active) return;
        Status = CustomerStatus.Active;
        Raise(new CustomerReactivatedDomainEvent(Id));
    }
}
