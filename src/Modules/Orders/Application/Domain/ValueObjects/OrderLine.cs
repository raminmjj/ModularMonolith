using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Orders.Application.Domain.ValueObjects;

public sealed class OrderLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public Guid? ReservationId { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine() { }

    internal static OrderLine Create(Guid productId, string productName, decimal unitPrice, int quantity, Guid? reservationId = null)
    {
        if (quantity <= 0) throw new DomainException("ORDER_LINE_QTY", "Quantity must be positive.");
        if (unitPrice < 0) throw new DomainException("ORDER_LINE_PRICE", "Unit price cannot be negative.");
        if (string.IsNullOrWhiteSpace(productName)) throw new DomainException("ORDER_LINE_NAME", "Product name cannot be empty.");

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity,
            ReservationId = reservationId,
        };
    }
}

public sealed record OrderStatus(string Value)
{
    public static readonly OrderStatus Pending = new("Pending");
    public static readonly OrderStatus Confirmed = new("Confirmed");
    public static readonly OrderStatus Shipped = new("Shipped");
    public static readonly OrderStatus Delivered = new("Delivered");
    public static readonly OrderStatus Cancelled = new("Cancelled");

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending.Value, Confirmed.Value, Shipped.Value, Delivered.Value, Cancelled.Value
    };

    public static OrderStatus Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || !Allowed.Contains(input))
            throw new DomainException("ORDER_STATUS_INVALID", $"'{input}' is not a valid order status.");
        return new OrderStatus(input);
    }
}
