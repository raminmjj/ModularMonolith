using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Supplier.Application.Domain.Suppliers;
using SupplierAggregate = ModularMonolith.Modules.Supplier.Application.Domain.Suppliers.Supplier;
using ModularMonolith.Modules.Supplier.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierAggregate>
{
    public void Configure(EntityTypeBuilder<SupplierAggregate> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        builder.Property(s => s.ContactEmail)
               .HasConversion(e => e.Value, v => Email.Create(v))
               .HasMaxLength(256).IsRequired();
        // EF Core 10: no member access through converted properties in HasIndex.
        builder.HasIndex("ContactEmail").IsUnique();

        builder.Property(s => s.PhoneNumber).HasMaxLength(30);
        builder.Property(s => s.Address).HasMaxLength(500);

        builder.Property(s => s.Status)
               .HasConversion(
                   v => v.Value,
                   v => v == SupplierStatus.Verified.Value ? SupplierStatus.Verified
                       : v == SupplierStatus.Suspended.Value ? SupplierStatus.Suspended
                       : SupplierStatus.Pending)
               .HasMaxLength(20).IsRequired();

        builder.Property(s => s.IsVerified).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.OwnsMany(s => s.Agreements, a =>
        {
            a.ToTable("BrandSupplyAgreements");
            a.WithOwner().HasForeignKey(x => x.SupplierId);
            a.HasKey(x => x.Id);
            // Client-generated Guid key — MUST be ValueGeneratedNever, otherwise new
            // agreements are tracked as Modified and SaveChanges throws (efcore#27736).
            a.Property(x => x.Id).ValueGeneratedNever();
            a.Property(x => x.BrandId).IsRequired();
            a.HasIndex(x => new { x.SupplierId, x.BrandId });
            a.Property(x => x.CommissionRate).HasPrecision(5, 2).IsRequired();
            a.Property(x => x.AssignedAt).IsRequired();
            a.Property(x => x.IsActive).IsRequired();
        });

        builder.Ignore(s => s.DomainEvents);
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
