using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Modules.Supplier.Application.Domain.Events;
using ModularMonolith.Modules.Supplier.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Supplier.Application.Domain.Suppliers;

/// <summary>
/// Supplier aggregate — OWNS the BrandSupplyAgreement relationship (ADR-0009).
/// Agreements reference Brands by Id only; existence is verified via ACL before
/// mutation. Invariants: agreements require a Verified supplier; one ACTIVE
/// agreement per brand; commission rate within 0–100.
/// </summary>
public sealed class Supplier : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public Email ContactEmail { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public string? Address { get; private set; }
    public SupplierStatus Status { get; private set; } = null!;
    public bool IsVerified { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<BrandSupplyAgreement> _agreements = [];
    public IReadOnlyCollection<BrandSupplyAgreement> Agreements => _agreements.AsReadOnly();

    private Supplier() { }

    public static Supplier Create(string name, string contactEmail, string? phoneNumber, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("SUPPLIER_NAME_REQUIRED", "Supplier name is required.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            ContactEmail = Email.Create(contactEmail),
            PhoneNumber = phoneNumber?.Trim(),
            Address = address?.Trim(),
            Status = SupplierStatus.Pending,
            IsVerified = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        supplier.Raise(new SupplierCreatedDomainEvent(supplier.Id));
        return supplier;
    }

    /// <summary>Pending → Verified. Only verified suppliers may hold brand agreements.</summary>
    public void Verify()
    {
        if (Status == SupplierStatus.Verified) return;
        if (Status == SupplierStatus.Suspended)
            throw new DomainException("SUPPLIER_SUSPENDED", "Suspended suppliers cannot be verified.");

        Status = SupplierStatus.Verified;
        IsVerified = true;
        Raise(new SupplierVerifiedDomainEvent(Id));
    }

    /// <summary>Verified → Suspended (verification revoked).</summary>
    public void Suspend()
    {
        if (Status == SupplierStatus.Suspended) return;
        if (Status != SupplierStatus.Verified)
            throw new DomainException("SUPPLIER_NOT_VERIFIED", "Only verified suppliers can be suspended.");

        Status = SupplierStatus.Suspended;
        IsVerified = false;
        Raise(new SupplierSuspendedDomainEvent(Id));
    }

    public void UpdateProfile(string name, string? phoneNumber, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("SUPPLIER_NAME_REQUIRED", "Supplier name is required.");
        Name = name.Trim();
        PhoneNumber = phoneNumber?.Trim();
        Address = address?.Trim();
        Raise(new SupplierProfileUpdatedDomainEvent(Id));
    }

    /// <summary>Requires Verified status + no duplicate active agreement for the brand.</summary>
    public BrandSupplyAgreement AddBrandAgreement(Guid brandId, decimal commissionRate, DateTimeOffset assignedAt)
    {
        if (!IsVerified)
            throw new DomainException("SUPPLIER_NOT_VERIFIED", "Brand agreements require a verified supplier.");
        if (_agreements.Any(a => a.BrandId == brandId && a.IsActive))
            throw new DomainException("BRAND_ALREADY_SUPPLIED", "An active agreement for this brand already exists.");

        var agreement = BrandSupplyAgreement.Create(Id, brandId, commissionRate, assignedAt);
        _agreements.Add(agreement);
        Raise(new BrandAgreementAssignedDomainEvent(Id, brandId, commissionRate));
        return agreement;
    }

    /// <summary>Deactivates the active agreement for a brand. No-op-safe when none exists.</summary>
    public void RemoveBrandAgreement(Guid brandId)
    {
        var agreement = _agreements.FirstOrDefault(a => a.BrandId == brandId && a.IsActive);
        if (agreement is null)
            throw new DomainException("AGREEMENT_NOT_ACTIVE", "No active agreement exists for this brand.");

        agreement.Deactivate();
        Raise(new BrandAgreementRemovedDomainEvent(Id, brandId));
    }
}
