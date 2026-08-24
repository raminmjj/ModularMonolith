using ModularMonolith.DDD.Common;

namespace ModularMonolith.SharedKernel.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }
    private Email(string value) => Value = value;

    public static Email Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new DomainException("EMAIL_EMPTY", "Email cannot be empty.");
        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length > 256) throw new DomainException("EMAIL_TOO_LONG", "Email cannot exceed 256 characters.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new DomainException("EMAIL_INVALID", $"'{input}' is not a valid email.");
        return new Email(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}
