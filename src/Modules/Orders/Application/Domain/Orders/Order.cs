using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Orders.Application.Domain.Events;
using ModularMonolith.Modules.Orders.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Orders.Application.Domain.Orders;

public sealed class Order : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }
    public OrderStatus Status { get; private set; } = null!;

    private readonly List<OrderLine> _lines = new();
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public decimal TotalAmount { get; private set; }

    private Order() { }

    public static Order Place(
        Guid userId,
        IEnumerable<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, Guid ReservationId)> lines)
    {
        if (userId == Guid.Empty) throw new DomainException("ORDER_USER_REQUIRED", "User id is required.");

        var lineList = lines?.ToList() ?? new();
        if (lineList.Count == 0) throw new DomainException("ORDER_EMPTY", "Order must have at least one line.");
        if (lineList.Any(l => l.ReservationId == Guid.Empty))
            throw new DomainException("ORDER_RESERVATION_REQUIRED", "Every line must have a valid reservation id.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlacedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Pending,
        };

        foreach (var l in lineList)
            order._lines.Add(OrderLine.Create(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity, l.ReservationId));

        order.RecalculateTotal();
        order.Raise(new OrderPlacedDomainEvent(order.Id, order.UserId, order.TotalAmount));
        return order;
    }

    private void RecalculateTotal() => TotalAmount = _lines.Sum(l => l.LineTotal);

    public IEnumerable<(Guid ProductId, Guid ReservationId)> GetReservationsToCommit()
        => _lines.Select(l => (l.ProductId, l.ReservationId!.Value));

    public IEnumerable<(Guid ProductId, Guid ReservationId)> GetReservationsToRelease()
        => _lines.Select(l => (l.ProductId, l.ReservationId!.Value));

    public void Confirm()
    {
        EnsureTransition(Status.Value, OrderStatus.Pending.Value, OrderStatus.Confirmed.Value);
        Status = OrderStatus.Confirmed;
        Raise(new OrderStatusChangedDomainEvent(Id, Status.Value));
    }

    public void Ship()
    {
        EnsureTransition(Status.Value, OrderStatus.Confirmed.Value, OrderStatus.Shipped.Value);
        Status = OrderStatus.Shipped;
        Raise(new OrderStatusChangedDomainEvent(Id, Status.Value));
    }

    public void Deliver()
    {
        EnsureTransition(Status.Value, OrderStatus.Shipped.Value, OrderStatus.Delivered.Value);
        Status = OrderStatus.Delivered;
        Raise(new OrderStatusChangedDomainEvent(Id, Status.Value));
    }

    public void Cancel()
    {
        if (Status.Value is "Shipped" or "Delivered")
            throw new DomainException("ORDER_CANCEL_NOT_ALLOWED", $"Cannot cancel order in '{Status.Value}' status.");
        Status = OrderStatus.Cancelled;
        Raise(new OrderStatusChangedDomainEvent(Id, Status.Value));
    }

    private static void EnsureTransition(string current, string expected, string target)
    {
        if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("ORDER_INVALID_TRANSITION", $"Cannot transition from '{current}' to '{target}'.");
    }
}
