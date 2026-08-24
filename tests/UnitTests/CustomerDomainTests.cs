using AwesomeAssertions;
using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;
using Xunit;

namespace ModularMonolith.UnitTests;

public class CustomerDomainTests
{
    [Fact]
    public void Register_Creates_Active_Customer()
    {
        var c = Customer.Register(Guid.NewGuid(), "Alice");
        c.Status.Value.Should().Be("Active");
        c.AccountTier.Should().Be("Standard");
    }

    [Fact]
    public void Register_Without_IdentityUser_Is_Rejected()
    {
        var act = () => Customer.Register(Guid.Empty, "Alice");
        act.Should().Throw<DomainException>().Where(e => e.Code == "CUSTOMER_IDENTITY_REQUIRED");
    }

    [Fact]
    public void SavedPaymentMethod_Must_Be_A_Vault_Token()
    {
        var c = Customer.Register(Guid.NewGuid(), "Alice");
        // Raw PAN-shaped value must be rejected BY THE DOMAIN, not just at the edge.
        var act = () => c.AddSavedPaymentMethod("4111111111111111", "Visa", new DateOnly(2029, 12, 31));
        act.Should().Throw<DomainException>().Where(e => e.Code == "PAYMENT_METHOD_TOKEN_INVALID");

        var method = c.AddSavedPaymentMethod("tok_visa_abc123", "Visa", new DateOnly(2029, 12, 31));
        method.TokenizedCard.Should().StartWith("tok_");
        method.IsDefault.Should().BeTrue(); // first saved method becomes default
    }

    [Fact]
    public void Suspend_Reactivate_RoundTrip()
    {
        var c = Customer.Register(Guid.NewGuid(), "Alice");
        c.Suspend();
        c.Status.Value.Should().Be("Suspended");
        c.Suspend(); // idempotent
        c.Reactivate();
        c.Status.Value.Should().Be("Active");
    }

    [Fact]
    public void Address_Validates_Country_Code()
    {
        var act = () => Address.Create("Main St 1", "Springfield", "12345", "USA");
        act.Should().Throw<DomainException>().Where(e => e.Code == "ADDRESS_COUNTRY_INVALID");
    }
}
