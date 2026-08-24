using AwesomeAssertions;
using ModularMonolith.DDD.Common;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Orders.Application.Domain.Orders;
using ModularMonolith.Modules.Orders.Application.Domain.ValueObjects;
using Xunit;

namespace ModularMonolith.UnitTests;

/// <summary>Order aggregate state machine — the write-side invariants.</summary>
public class OrderStateTests
{
    private static Order PlacedOrder() => Order.Place(
        Guid.NewGuid(),
        [(Guid.NewGuid(), "Widget", 9.99m, 2, Guid.NewGuid())]);

    [Fact]
    public void Place_With_Valid_Lines_Starts_Pending()
    {
        var order = PlacedOrder();
        order.Status.Value.Should().Be("Pending");
        order.TotalAmount.Should().Be(19.98m);
    }

    [Fact]
    public void Place_Without_Reservations_Is_Rejected()
    {
        var act = () => Order.Place(Guid.NewGuid(), [(Guid.NewGuid(), "Widget", 9.99m, 1, Guid.Empty)]);
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_RESERVATION_REQUIRED");
    }

    [Fact]
    public void Place_With_Empty_Lines_Is_Rejected()
    {
        var act = () => Order.Place(Guid.NewGuid(), []);
        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_EMPTY");
    }

    [Theory]
    [InlineData("Pending", "Confirmed")]
    [InlineData("Confirmed", "Shipped")]
    [InlineData("Shipped", "Delivered")]
    public void Happy_Path_Transitions_Are_Allowed(string from, string to)
    {
        var order = PlacedOrder();
        if (from == "Confirmed") order.Confirm();
        if (from == "Shipped") { order.Confirm(); order.Ship(); }

        switch (to)
        {
            case "Confirmed": order.Confirm(); break;
            case "Shipped": order.Ship(); break;
            case "Delivered": order.Deliver(); break;
        }

        order.Status.Value.Should().Be(to);
    }

    [Theory]
    [InlineData("Pending", "Shipped")]   // must confirm first
    [InlineData("Pending", "Delivered")] // must confirm + ship first
    [InlineData("Shipped", "Cancelled")] // too late to cancel
    [InlineData("Delivered", "Cancelled")]
    public void Invalid_Transitions_Are_Rejected(string fromStateSetup, string attempted)
    {
        var order = PlacedOrder();
        if (fromStateSetup is "Confirmed" or "Shipped" or "Delivered") order.Confirm();
        if (fromStateSetup is "Shipped" or "Delivered") order.Ship();
        if (fromStateSetup == "Delivered") order.Deliver();

        var act = () =>
        {
            switch (attempted)
            {
                case "Confirmed": order.Confirm(); break;
                case "Shipped": order.Ship(); break;
                case "Delivered": order.Deliver(); break;
                case "Cancelled": order.Cancel(); break;
            }
        };

        act.Should().Throw<DomainException>().Where(e => e.Code == "ORDER_INVALID_TRANSITION"
                                                      || e.Code == "ORDER_CANCEL_NOT_ALLOWED");
    }
}
