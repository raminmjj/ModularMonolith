using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Brand.Application.Domain.ValueObjects;

/// <summary>URL-friendly brand identifier: lowercase letters/digits/hyphens.</summary>
public sealed class Slug : ValueObject
{
    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DomainException("SLUG_REQUIRED", "Slug is required.");

        var normalized = input.Trim().ToLowerInvariant().Replace(' ', '-');
        if (normalized.Length is < 2 or > 60)
            throw new DomainException("SLUG_LENGTH_INVALID", "Slug must be 2–60 characters.");
        if (!normalized.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new DomainException("SLUG_INVALID", "Slug may contain only ASCII letters, digits and hyphens.");

        return new Slug(normalized);
    }

    public static Slug FromName(string name) => Create(name);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public sealed class BrandStatus : ValueObject
{
    public static readonly BrandStatus PendingReview = new("PendingReview");
    public static readonly BrandStatus Approved = new("Approved");
    public static readonly BrandStatus Rejected = new("Rejected");

    public string Value { get; }
    private BrandStatus(string value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
