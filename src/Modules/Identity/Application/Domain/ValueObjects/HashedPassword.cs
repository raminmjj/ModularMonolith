using ModularMonolith.DDD.Common;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Identity.Application.Domain.ValueObjects;

public sealed class HashedPassword : ValueObject
{
    public string Hash { get; }
    public string Salt { get; }
    private HashedPassword(string hash, string salt) { Hash = hash; Salt = salt; }

    public static HashedPassword Create(string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new DomainException("PASSWORD_HASH_EMPTY", "Password hash cannot be empty.");
        if (string.IsNullOrWhiteSpace(salt)) throw new DomainException("PASSWORD_SALT_EMPTY", "Password salt cannot be empty.");
        return new HashedPassword(hash, salt);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Hash; yield return Salt; }
}
