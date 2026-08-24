namespace ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Dtos;

public sealed record RegisterCustomerRequest(Guid IdentityUserId, string DisplayName);
public sealed record AddAddressRequest(string Street, string City, string PostalCode, string Country);
public sealed record SavePaymentMethodRequest(string TokenizedCard, string CardType, DateOnly ExpiryDate);

public sealed record CustomerCreatedResponse(Guid Id);
