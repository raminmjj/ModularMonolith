using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Supplier.Application.Domain.ValueObjects;

public sealed class SupplierStatus : ValueObject
{
    public static readonly SupplierStatus Pending = new("Pending");
    public static readonly SupplierStatus Verified = new("Verified");
    public static readonly SupplierStatus Suspended = new("Suspended");

    public string Value { get; }
    private SupplierStatus(string value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
