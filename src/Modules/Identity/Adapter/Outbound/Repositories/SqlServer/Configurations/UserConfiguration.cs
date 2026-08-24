using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Identity.Application.Domain.Users;
using ModularMonolith.Modules.Identity.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
               .HasConversion(e => e.Value, v => Email.Create(v))
               .HasMaxLength(256).IsRequired();
        // EF Core 10: member access through a converted property (u.Email.Value)
        // is not valid in HasIndex — target the mapped column name instead.
        builder.HasIndex("Email").IsUnique();

        builder.OwnsOne(u => u.Password, p =>
        {
            p.Property(pp => pp.Hash).HasMaxLength(512).HasColumnName("PasswordHash");
            p.Property(pp => pp.Salt).HasMaxLength(128).HasColumnName("PasswordSalt");
            p.WithOwner();
        });

        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.LastLoginAt);
        builder.Property(u => u.FailedLoginAttempts).IsRequired();
        builder.Property(u => u.LockedUntil);

        builder.Property<List<string>>("_roles").HasColumnName("Roles")
               .HasConversion(v => string.Join(',', v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        builder.Ignore(u => u.DomainEvents);
        builder.Property(u => u.RowVersion).IsRowVersion();
    }
}
