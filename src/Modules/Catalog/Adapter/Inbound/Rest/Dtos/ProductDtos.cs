namespace ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Dtos;

public sealed record CreateProductRequest(string Sku, string Name, decimal Price, string Currency, int InitialStock, string? Description);
public sealed record ProductResponse(Guid Id, string Sku, string Name, string? Description, decimal Price, string Currency, int Stock, int ReservedStock, int AvailableStock, bool IsActive);
public sealed record AdjustStockRequest(Guid ProductId, int Delta);
public sealed record ChangePriceRequest(Guid ProductId, decimal NewPrice, string Currency);
public sealed record ReserveStockRequest(int Quantity, int TtlMinutes);
