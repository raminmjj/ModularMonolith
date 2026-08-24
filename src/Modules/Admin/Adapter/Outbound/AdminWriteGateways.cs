using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound;
using ModularMonolith.Modules.Orders.Application.Ports.Inbound;
using ModularMonolith.Modules.Payment.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Outbound;

// ─── WRITE-side ACL gateways (ADR-0007 amendment: domain-owned admin operations) ───
// Each gateway delegates to the OWNING module's dedicated admin port
// (Catalog.ICatalogAdminService, …) — NOT to raw public services. The domain module
// owns the capability; Admin only reaches it across the boundary.

internal sealed class CatalogAdminGateway(Catalog.Application.Ports.Inbound.ICatalogAdminService catalog) : ICatalogAdminGateway
{
    public async Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency,
        int initialStock, string? description, CancellationToken ct = default)
    {
        var result = await catalog.CreateProductAsync(sku, name, price, currency, initialStock, description, ct);
        return result.IsSuccess ? Result.Success(result.Value!) : Result.Failure<Guid>(result.Error);
    }

    public Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default)
        => catalog.ChangePriceAsync(productId, newPrice, currency, ct);

    public Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default)
        => catalog.AdjustStockAsync(productId, delta, ct);

    public Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default)
        => catalog.DeactivateProductAsync(productId, ct);
}

internal sealed class CustomerAdminGateway(Customer.Application.Ports.Inbound.ICustomerAdminService customers) : ICustomerAdminGateway
{
    public Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default)
        => customers.SuspendAsync(customerId, ct);

    public Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default)
        => customers.ReactivateAsync(customerId, ct);
}

internal sealed class OrdersAdminGateway(Orders.Application.Ports.Inbound.IOrderAdminService orders) : IOrdersAdminGateway
{
    public Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default)
        => orders.ChangeStatusAsync(orderId, newStatus, ct);
}

internal sealed class PaymentAdminGateway(Payment.Application.Ports.Inbound.IPaymentAdminService payments) : IPaymentAdminGateway
{
    public Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
        => payments.CaptureAsync(paymentId, ct);

    public Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default)
        => payments.FailAsync(paymentId, reason, ct);

    public Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default)
        => payments.RefundAsync(paymentId, ct);
}
