using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound;
using ModularMonolith.Modules.Orders.Application.Ports.Inbound;
using ModularMonolith.Modules.Payment.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Outbound;

// ─── WRITE-side ACL gateways (ADR-0007) ───
// Delegate DIRECTLY to provider inbound ports — the same sanctioned pattern as the
// Orders→Catalog and Payment→Customer gateways. Arch rule: ONLY this adapter may
// reference provider write-side Applications; Admin.Application + GraphQL stay blind.

internal sealed class CatalogAdminGateway(IProductService products) : ICatalogAdminProvider
{
    public async Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency,
        int initialStock, string? description, CancellationToken ct = default)
    {
        var result = await products.CreateProductAsync(sku, name, price, currency, initialStock, description, ct);
        return result.IsSuccess ? Result.Success(result.Value!.Id) : Result.Failure<Guid>(result.Error);
    }

    public Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default)
        => products.ChangePriceAsync(productId, newPrice, currency, ct);

    public Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default)
        => products.AdjustStockAsync(productId, delta, ct);

    public Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default)
        => products.DeactivateProductAsync(productId, ct);
}

internal sealed class CustomerAdminGateway(ICustomerInboundPort customers) : ICustomerAdminProvider
{
    public Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default)
        => customers.SuspendAsync(customerId, ct);

    public Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default)
        => customers.ReactivateAsync(customerId, ct);
}

internal sealed class OrdersAdminGateway(IOrderService orders) : IOrdersAdminProvider
{
    public Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
        => orders.ChangeStatusAsync(orderId, newStatus, ct);
}

internal sealed class PaymentAdminGateway(IPaymentService payments) : IPaymentAdminProvider
{
    public Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
        => payments.CaptureAsync(paymentId, ct);

    public Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
        => payments.FailAsync(paymentId, reason, ct);

    public Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
        => payments.RefundAsync(paymentId, ct);
}
