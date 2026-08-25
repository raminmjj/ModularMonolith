using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Admin.Application.Ports.Outbound;

// ─── Admin write-side GATEWAY ports (ADR-0007 amendment: domain-owned operations) ───
// Mirrors of the domain modules' own admin ports (Catalog.ICatalogAdminService etc.).
// Implemented in Adapter/Outbound by delegating DIRECTLY to those domain admin
// services. The Admin module adds no domain logic here — by design.

public interface ICatalogAdminGateway
{
    Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default);
    Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default);
    Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default);
    Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default);
}

public interface ICustomerAdminGateway
{
    Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default);
    Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default);
}

public interface IOrdersAdminGateway
{
    Task<Result> ChangeStatusAsync(Guid orderId, string newStatus, CancellationToken ct = default);
}

public interface IPaymentAdminGateway
{
    Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
    Task<Result> RefundAsync(Guid paymentId, CancellationToken ct = default);
}

/// <summary>Mirrors Supplier.ICatalogAdminService-style admin port (ADR-0009).</summary>
public interface ISupplierAdminGateway
{
    Task<Result> VerifyAsync(Guid supplierId, CancellationToken ct = default);
    Task<Result> SuspendAsync(Guid supplierId, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid supplierId, string name, string? phoneNumber, string? address, CancellationToken ct = default);
    /// <summary>Cross-module guard (brand existence) is enforced INSIDE the Supplier module.</summary>
    Task<Result> AssignBrandAsync(Guid supplierId, Guid brandId, decimal commissionRate, CancellationToken ct = default);
    Task<Result> RemoveBrandAsync(Guid supplierId, Guid brandId, CancellationToken ct = default);
}

/// <summary>Mirrors Brand.IBrandAdminService (ADR-0009).</summary>
public interface IBrandAdminGateway
{
    Task<Result<Guid>> CreateBrandAsync(string name, string? slug, string? description, string? logoUrl, string countryOfOrigin, CancellationToken ct = default);
    Task<Result> ApproveBrandAsync(Guid brandId, CancellationToken ct = default);
    Task<Result> RejectBrandAsync(Guid brandId, string reason, CancellationToken ct = default);
    Task<Result> UpdateBrandDetailsAsync(Guid brandId, string name, string? description, string? logoUrl, string countryOfOrigin, CancellationToken ct = default);
}
