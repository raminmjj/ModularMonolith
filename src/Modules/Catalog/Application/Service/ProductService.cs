using FluentValidation;
using Microsoft.Extensions.Logging;
using ModularMonolith.Contracts.Catalog;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Domain.Products;
using ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Catalog.Application.Ports.Outbound;
using ModularMonolith.Modules.Catalog.Application.Validation;
using ModularMonolith.DDD.Common;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Catalog.Application.Service;

/// <summary>
/// Inbound port implementation — the hexagon's use case handler. All business
/// logic lives in the Domain aggregate; this class orchestrates: validate →
/// load → call domain method → save.
///
/// FluentValidation runs at the hexagon boundary — BEFORE domain operations.
/// This is Hexagonal Best Practice: adapters (REST, gRPC) don't validate;
/// the hexagon validates its own inputs.
/// </summary>
public sealed class ProductService : Ports.Inbound.IProductService
{
    private static readonly TimeSpan DefaultReservationTtl = TimeSpan.FromMinutes(10);

    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProductService> _logger;

    // Validators are injected via DI — see DependencyInjection.cs
    private readonly CreateProductValidator _createProductValidator;
    private readonly ChangePriceValidator _changePriceValidator;
    private readonly ReserveStockValidator _reserveStockValidator;

    public ProductService(
        IProductRepository products,
        IUnitOfWork unitOfWork,
        ILogger<ProductService> logger,
        CreateProductValidator createProductValidator,
        ChangePriceValidator changePriceValidator,
        ReserveStockValidator reserveStockValidator)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _createProductValidator = createProductValidator;
        _changePriceValidator = changePriceValidator;
        _reserveStockValidator = reserveStockValidator;
    }

    public async Task<Result<ProductSnapshot>> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure<ProductSnapshot>(new Error("PRODUCT_NOT_FOUND", "Product was not found."));
        return Result.Success(new ProductSnapshot(product.Id, product.Name, product.Price.Amount, product.AvailableStock));
    }

    public async Task<Result<ProductSnapshot>> CreateProductAsync(
        string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default)
    {
        // Hexagon boundary validation — BEFORE domain operations
        var validationResult = await _createProductValidator.ValidateAsync((sku, name, price, currency, initialStock, description), ct);
        if (!validationResult.IsValid)
            return Result.Failure<ProductSnapshot>(new Error("VALIDATION", string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))));

        var skuVo = Sku.Create(sku);
        if (await _products.SkuExistsAsync(skuVo.Value, ct))
            return Result.Failure<ProductSnapshot>(new Error("SKU_TAKEN", $"SKU '{skuVo.Value}' already exists."));

        var priceVo = Money.Create(price, currency);
        var product = Product.Create(skuVo, name, priceVo, initialStock, description);

        await _products.AddAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new ProductSnapshot(product.Id, product.Name, product.Price.Amount, product.AvailableStock));
    }

    public async Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default)
    {
        // Hexagon boundary validation
        var validationResult = await _changePriceValidator.ValidateAsync((productId, newPrice, currency), ct);
        if (!validationResult.IsValid)
            return Result.Failure(new Error("VALIDATION", string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))));

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        product.ChangePrice(Money.Create(newPrice, currency));
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        product.AdjustStock(delta);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ReservationSnapshot>> ReserveStockAsync(Guid productId, int quantity, TimeSpan ttl, CancellationToken ct = default)
    {
        // Hexagon boundary validation
        var validationResult = await _reserveStockValidator.ValidateAsync((productId, quantity), ct);
        if (!validationResult.IsValid)
            return Result.Failure<ReservationSnapshot>(new Error("VALIDATION", string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))));

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure<ReservationSnapshot>(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        var reservationId = Guid.NewGuid();
        try
        {
            product.ReserveStock(quantity, reservationId, ttl == TimeSpan.Zero ? DefaultReservationTtl : ttl);
        }
        catch (DomainException dex)
        {
            return Result.Failure<ReservationSnapshot>(new Error(dex.Code, dex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new ReservationSnapshot(reservationId, product.Id, quantity, DateTimeOffset.UtcNow + ttl));
    }

    public async Task<Result> CommitReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        try { product.CommitReservation(reservationId); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReleaseReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null) return Result.Failure(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        product.ReleaseReservation(reservationId);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
