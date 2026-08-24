using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string postalCode, string country)
        => (Street, City, PostalCode, Country) = (street, city, postalCode, country);

    public static Address Create(string street, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new DomainException("ADDRESS_STREET_REQUIRED", "Street is required.");
        if (string.IsNullOrWhiteSpace(city)) throw new DomainException("ADDRESS_CITY_REQUIRED", "City is required.");
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new DomainException("ADDRESS_COUNTRY_INVALID", "Country must be an ISO-3166 alpha-2 code.");
        return new Address(street.Trim(), city.Trim(), postalCode.Trim(), country.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
}

/// <summary>Smart enum — account standing drives Payment's ACL verification.</summary>
public sealed class CustomerStatus : ValueObject
{
    public static readonly CustomerStatus Active = new("Active");
    public static readonly CustomerStatus Suspended = new("Suspended");

    public string Value { get; }
    private CustomerStatus(string value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
