using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;
using CustomerAggregate = ModularMonolith.Modules.Customer.Application.Domain.Customers.Customer;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerAggregate>
{
    public void Configure(EntityTypeBuilder<CustomerAggregate> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.IdentityUserId).IsRequired();
        // One wallet per identity user — enforced at the database.
        builder.HasIndex("IdentityUserId").IsUnique();

        builder.Property(c => c.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.AccountTier).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Status)
               .HasConversion(s => s.Value, v => CustomerStatus.Active.Value == v ? CustomerStatus.Active : CustomerStatus.Suspended)
               .HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.OwnsMany(c => c.Addresses, a =>
        {
            a.ToTable("CustomerAddresses");
            a.WithOwner().HasForeignKey("CustomerId");
            a.Property<Guid>("Id");
            a.Property(x => x.Street).HasMaxLength(200).IsRequired();
            a.Property(x => x.City).HasMaxLength(100).IsRequired();
            a.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
            a.Property(x => x.Country).HasMaxLength(2).IsRequired();
        });

        builder.OwnsMany(c => c.PaymentMethods, m =>
        {
            m.ToTable("CustomerPaymentMethods");
            m.WithOwner().HasForeignKey(x => x.CustomerId);
            m.HasKey(x => x.Id);
            // Client-generated Guid key — MUST be ValueGeneratedNever, otherwise EF
            // tracks newly added methods as Modified and SaveChanges throws
            // (dotnet/efcore#27736 — see AGENTS.md pitfalls).
            m.Property(x => x.Id).ValueGeneratedNever();
            m.Property(x => x.TokenizedCard).HasMaxLength(200).IsRequired(); // vault token only
            m.Property(x => x.CardType).HasMaxLength(20).IsRequired();
            m.Property(x => x.ExpiryDate).IsRequired();
            m.Property(x => x.IsDefault).IsRequired();
        });

        builder.Ignore(c => c.DomainEvents);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}
