using AwesomeAssertions;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;
using ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;
using Xunit;

namespace ModularMonolith.UnitTests;

public class PaymentDomainTests
{
    private static PaymentTransaction NewPending() =>
        PaymentTransaction.Initiate(
            Guid.NewGuid(), Guid.NewGuid(), Money.Create(25m, "USD"),
            PaymentMethodSnapshot.FromSaved(new SavedPaymentMethodDto(Guid.NewGuid(), "tok_visa_xyz", new DateOnly(2030, 1, 1), "Visa")));

    [Fact]
    public void Snapshot_Rejects_Raw_Card_Numbers()
    {
        var act = () => PaymentMethodSnapshot.FromNewCardToken("4111111111111111", "Visa", new DateOnly(2030, 1, 1));
        act.Should().Throw<DomainException>().Where(e => e.Code == "PAYMENT_TOKEN_INVALID");
    }

    [Fact]
    public void Initiate_Starts_Pending_With_Token_Snapshot()
    {
        var tx = NewPending();
        tx.Status.Value.Should().Be("Pending");
        tx.Method.Token.Should().StartWith("tok_");
    }

    [Fact]
    public void Initiate_Rejects_NonPositive_Amount()
    {
        var act = () => PaymentTransaction.Initiate(
            Guid.NewGuid(), Guid.NewGuid(), Money.Create(0m, "USD"),
            PaymentMethodSnapshot.FromNewCardToken("tok_x", "Visa", new DateOnly(2030, 1, 1)));
        act.Should().Throw<DomainException>().Where(e => e.Code == "AMOUNT_POSITIVE_REQUIRED");
    }

    [Fact]
    public void Pending_Transition_Table()
    {
        var tx = NewPending();
        tx.MarkCaptured();
        tx.Status.Value.Should().Be("Captured");

        var fail = () => tx.MarkFailed("declined"); // terminal state
        fail.Should().Throw<DomainException>().Where(e => e.Code == "PAYMENT_NOT_PENDING");

        var again = () => tx.MarkCaptured();
        again.Should().Throw<DomainException>().Where(e => e.Code == "PAYMENT_NOT_PENDING");
    }
}
