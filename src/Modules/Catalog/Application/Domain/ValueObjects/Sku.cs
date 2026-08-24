using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;

public sealed class Sku : ValueObject
{
    public string Value { get; }
    private Sku(string value) => Value = value;

    public static Sku Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new DomainException("SKU_EMPTY", "SKU cannot be empty.");
        var normalized = input.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 32) throw new DomainException("SKU_LENGTH", "SKU must be between 3 and 32 characters.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[A-Z0-9\-]+$"))
            throw new DomainException("SKU_FORMAT", "SKU may only contain letters, digits and hyphens.");
        return new Sku(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}
