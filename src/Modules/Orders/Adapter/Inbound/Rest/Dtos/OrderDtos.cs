namespace ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Dtos;

public sealed record PlaceOrderRequest(Guid UserId, IEnumerable<OrderLineInput> Lines);
public sealed record OrderLineInput(Guid ProductId, int Quantity);
public sealed record OrderResponse(Guid Id, Guid UserId, DateTimeOffset PlacedAt, string Status, decimal TotalAmount);
