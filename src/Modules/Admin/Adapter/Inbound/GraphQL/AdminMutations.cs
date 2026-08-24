using HotChocolate.Authorization;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Inbound;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;

// ─── MUTATION ROOT EXTENSIONS (ADR-0007, Decision #2: all writes in GraphQL) ───
// Every mutation delegates to an admin service → ACL gateway port → provider
// inbound port. Resolvers map Result → GraphQL errors; no logic here.

[ExtendObjectType("Mutation")]
public sealed class AdminCatalogMutations
{
    /// <summary>Create a catalog product.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<Guid> CreateProduct(
        string sku, string name, decimal price, string currency, int initialStock, string? description,
        [Service] ICatalogAdminGateway gateway, CancellationToken ct)
        => (await gateway.CreateProductAsync(sku, name, price, currency, initialStock, description, ct)).ThrowIfFailure();

    /// <summary>Change a product's price.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ChangeProductPrice(
        Guid productId, decimal newPrice, string currency,
        [Service] ICatalogAdminGateway gateway, CancellationToken ct)
    { (await gateway.ChangePriceAsync(productId, newPrice, currency, ct)).ThrowIfFailure(); return true; }

    /// <summary>Adjust stock by delta (positive or negative).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> AdjustProductStock(
        Guid productId, int delta,
        [Service] ICatalogAdminGateway gateway, CancellationToken ct)
    { (await gateway.AdjustStockAsync(productId, delta, ct)).ThrowIfFailure(); return true; }

    /// <summary>Soft-delete: deactivate a product (stays queryable for history).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> DeactivateProduct(
        Guid productId, [Service] ICatalogAdminGateway gateway, CancellationToken ct)
    { (await gateway.DeactivateProductAsync(productId, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminCustomerMutations
{
    /// <summary>Suspend a customer — blocks new payments via the Payment ACL.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> SuspendCustomer(
        Guid customerId, [Service] ICustomerAdminGateway gateway, CancellationToken ct)
    { (await gateway.SuspendAsync(customerId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Reactivate a suspended customer.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ReactivateCustomer(
        Guid customerId, [Service] ICustomerAdminGateway gateway, CancellationToken ct)
    { (await gateway.ReactivateAsync(customerId, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminOrderMutations
{
    /// <summary>Change order status (Pending→Confirmed→Shipped→Delivered / Cancel).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ChangeOrderStatus(
        Guid orderId, string status, [Service] IOrdersAdminGateway gateway, CancellationToken ct)
    { (await gateway.ChangeStatusAsync(orderId, status, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminPaymentMutations
{
    /// <summary>Capture a pending payment.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> CapturePayment(
        Guid paymentId, [Service] IPaymentAdminGateway gateway, CancellationToken ct)
    { (await gateway.CaptureAsync(paymentId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Refund a captured payment.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> RefundPayment(
        Guid paymentId, [Service] IPaymentAdminGateway gateway, CancellationToken ct)
    { (await gateway.RefundAsync(paymentId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Mark a pending payment as failed with a reason.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> FailPayment(
        Guid paymentId, string reason, [Service] IPaymentAdminGateway gateway, CancellationToken ct)
    { (await gateway.FailAsync(paymentId, reason, ct)).ThrowIfFailure(); return true; }
}
