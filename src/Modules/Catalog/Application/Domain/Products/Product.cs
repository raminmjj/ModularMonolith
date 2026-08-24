using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Catalog.Application.Domain.Events;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Catalog.Application.Domain.Products;

public sealed class Product : AggregateRoot<Guid>
{
    public Sku Sku { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Price { get; private set; } = null!;
    public int Stock { get; private set; }
    public int ReservedStock { get; private set; }
    public int AvailableStock => Stock - ReservedStock;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private readonly List<StockReservation> _reservations = new();
    public IReadOnlyCollection<StockReservation> Reservations => _reservations.AsReadOnly();

    private Product() { }

    public static Product Create(Sku sku, string name, Money price, int initialStock = 0, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("PRODUCT_NAME_EMPTY", "Product name cannot be empty.");
        if (name.Length > 200) throw new DomainException("PRODUCT_NAME_TOO_LONG", "Product name cannot exceed 200 characters.");
        if (initialStock < 0) throw new DomainException("PRODUCT_STOCK_NEGATIVE", "Initial stock cannot be negative.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name.Trim(),
            Description = description?.Trim(),
            Price = price,
            Stock = initialStock,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        product.Raise(new ProductCreatedDomainEvent(product.Id, product.Name, product.Price.Amount));
        if (initialStock > 0)
            product.Raise(new ProductStockAdjustedDomainEvent(product.Id, initialStock));

        return product;
    }

    public void ChangePrice(Money newPrice)
    {
        if (!IsActive) throw new DomainException("PRODUCT_INACTIVE", "Inactive product cannot be modified.");
        if (newPrice.Amount == Price.Amount) return;
        Price = newPrice;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductPriceChangedDomainEvent(Id, newPrice.Amount));
    }

    public void AdjustStock(int delta)
    {
        if (!IsActive) throw new DomainException("PRODUCT_INACTIVE", "Inactive product cannot be modified.");
        var newStock = Stock + delta;
        if (newStock < 0) throw new DomainException("PRODUCT_STOCK_NEGATIVE", "Resulting stock cannot be negative.");
        Stock = newStock;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductStockAdjustedDomainEvent(Id, newStock));
    }

    public StockReservation ReserveStock(int quantity, Guid reservationId, TimeSpan ttl)
    {
        if (!IsActive) throw new DomainException("PRODUCT_INACTIVE", "Inactive product cannot be reserved.");
        if (quantity <= 0) throw new DomainException("RESERVATION_QTY", "Quantity must be positive.");
        if (quantity > AvailableStock)
            throw new DomainException("STOCK_INSUFFICIENT", $"Only {AvailableStock} available.");

        var reservation = new StockReservation(reservationId, Id, quantity, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + ttl);
        _reservations.Add(reservation);
        ReservedStock += quantity;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductReservedDomainEvent(Id, quantity, reservationId));
        return reservation;
    }

    public void ReleaseReservation(Guid reservationId)
    {
        var r = _reservations.FirstOrDefault(x => x.Id == reservationId);
        if (r is null) return;
        _reservations.Remove(r);
        ReservedStock -= r.Quantity;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductReservationReleasedDomainEvent(Id, r.Quantity, reservationId));
    }

    public void CommitReservation(Guid reservationId)
    {
        var r = _reservations.FirstOrDefault(x => x.Id == reservationId);
        if (r is null) throw new DomainException("RESERVATION_NOT_FOUND", "Reservation does not exist.");
        _reservations.Remove(r);
        ReservedStock -= r.Quantity;
        Stock -= r.Quantity;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductStockAdjustedDomainEvent(Id, Stock));
    }

    /// <summary>
    /// Releases every reservation whose TTL has elapsed. Called by the
    /// ReservationExpirationService (BackgroundService) — see docs/adr/0005.
    /// Returns the ids that were released (empty when nothing expired).
    /// </summary>
    public IReadOnlyList<Guid> ReleaseExpiredReservations(DateTimeOffset utcNow)
    {
        List<Guid> released = [];
        foreach (var r in _reservations.Where(x => x.ExpiresAt <= utcNow).ToList())
        {
            _reservations.Remove(r);
            ReservedStock -= r.Quantity;
            released.Add(r.Id);
            Raise(new ProductReservationReleasedDomainEvent(Id, r.Quantity, r.Id));
        }
        if (released.Count > 0) UpdatedAt = utcNow;
        return released;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
        Raise(new ProductDeactivatedDomainEvent(Id));
    }
}

public sealed class StockReservation : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private StockReservation() { }

    internal StockReservation(Guid id, Guid productId, int quantity, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id; ProductId = productId; Quantity = quantity; CreatedAt = createdAt; ExpiresAt = expiresAt;
    }
}
