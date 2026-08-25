using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Supplier.Application.Domain.Suppliers;

/// <summary>
/// Join entity inside the Supplier aggregate (M2M ownership, ADR-0009).
/// References Brand by Id only — no brand data is duplicated.
/// </summary>
public sealed class BrandSupplyAgreement : Entity<Guid>
{
    public Guid SupplierId { get; private set; }
    public Guid BrandId { get; private set; }
    public decimal CommissionRate { get; private set; } // 0–100 (%)
    public DateTimeOffset AssignedAt { get; private set; }
    public bool IsActive { get; private set; }

    private BrandSupplyAgreement() { }

    internal static BrandSupplyAgreement Create(Guid supplierId, Guid brandId, decimal commissionRate, DateTimeOffset assignedAt)
    {
        if (brandId == Guid.Empty)
            throw new DomainException("AGREEMENT_BRAND_REQUIRED", "Brand id is required.");
        if (commissionRate is < 0 or > 100)
            throw new DomainException("COMMISSION_RATE_INVALID", "Commission rate must be between 0 and 100.");

        return new BrandSupplyAgreement
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            BrandId = brandId,
            CommissionRate = commissionRate,
            AssignedAt = assignedAt,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;
}
