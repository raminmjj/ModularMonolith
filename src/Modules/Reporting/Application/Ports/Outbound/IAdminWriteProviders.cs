using ModularMonolith.Contracts.Reporting;

namespace ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

// ─── Write-side ACL ports (ADR-0007): delegate to provider INBOUND PORTS only ───
// Implemented in Adapter/Outbound. GraphQL/Application never see provider write types.

public interface ICatalogAdminProvider
{
    Task<Framework.Results.Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default);
    Task<Framework.Results.Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default);
    Task<Framework.Results.Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default);
    Task<Framework.Results.Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default);
}

public interface ICustomerAdminProvider
{
    Task<Framework.Results.Result> SuspendAsync(Guid customerId, CancellationToken ct = default);
    Task<Framework.Results.Result> ReactivateAsync(Guid customerId, CancellationToken ct = default);
}

public interface IOrdersAdminProvider
{
    Task<Framework.Results.Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
}

public interface IPaymentAdminProvider
{
    Task<Framework.Results.Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Framework.Results.Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
    Task<Framework.Results.Result> RefundAsync(Guid paymentId, CancellationToken ct = default);
}
