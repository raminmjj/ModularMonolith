using HotChocolate.Authorization;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Inbound;

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
        [Service] IAdminCatalogService service, CancellationToken ct)
        => (await service.CreateProductAsync(sku, name, price, currency, initialStock, description, ct)).ThrowIfFailure();

    /// <summary>Change a product's price.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ChangeProductPrice(
        Guid productId, decimal newPrice, string currency,
        [Service] IAdminCatalogService service, CancellationToken ct)
    { (await service.ChangePriceAsync(productId, newPrice, currency, ct)).ThrowIfFailure(); return true; }

    /// <summary>Adjust stock by delta (positive or negative).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> AdjustProductStock(
        Guid productId, int delta,
        [Service] IAdminCatalogService service, CancellationToken ct)
    { (await service.AdjustStockAsync(productId, delta, ct)).ThrowIfFailure(); return true; }

    /// <summary>Soft-delete: deactivate a product (stays queryable for history).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> DeactivateProduct(
        Guid productId, [Service] IAdminCatalogService service, CancellationToken ct)
    { (await service.DeactivateProductAsync(productId, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminCustomerMutations
{
    /// <summary>Suspend a customer — blocks new payments via the Payment ACL.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> SuspendCustomer(
        Guid customerId, [Service] IAdminCustomerService service, CancellationToken ct)
    { (await service.SuspendAsync(customerId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Reactivate a suspended customer.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ReactivateCustomer(
        Guid customerId, [Service] IAdminCustomerService service, CancellationToken ct)
    { (await service.ReactivateAsync(customerId, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminOrderMutations
{
    /// <summary>Change order status (Pending→Confirmed→Shipped→Delivered / Cancel).</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> ChangeOrderStatus(
        Guid orderId, string status, [Service] IAdminOrderService service, CancellationToken ct)
    { (await service.ChangeStatusAsync(orderId, status, ct)).ThrowIfFailure(); return true; }
}

[ExtendObjectType("Mutation")]
public sealed class AdminPaymentMutations
{
    /// <summary>Capture a pending payment.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> CapturePayment(
        Guid paymentId, [Service] IAdminPaymentService service, CancellationToken ct)
    { (await service.CaptureAsync(paymentId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Refund a captured payment.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> RefundPayment(
        Guid paymentId, [Service] IAdminPaymentService service, CancellationToken ct)
    { (await service.RefundAsync(paymentId, ct)).ThrowIfFailure(); return true; }

    /// <summary>Mark a pending payment as failed with a reason.</summary>
    [Authorize(Policy = "Admin")]
    public async Task<bool> FailPayment(
        Guid paymentId, string reason, [Service] IAdminPaymentService service, CancellationToken ct)
    { (await service.FailAsync(paymentId, reason, ct)).ThrowIfFailure(); return true; }
}
