using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Catalog.Application.Domain.Products;
using ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;
using ModularMonolith.SharedKernel.ValueObjects;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku)
               .HasConversion(s => s.Value, v => Sku.Create(v))
               .HasMaxLength(32).IsRequired();
        // EF Core 10: member access through a converted property (p.Sku.Value)
        // is not valid in HasIndex — target the mapped column name instead.
        builder.HasIndex("Sku").IsUnique();

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.OwnsOne(p => p.Price, m =>
        {
            m.Property(mm => mm.Amount).HasColumnName("PriceAmount").HasPrecision(18, 2);
            m.Property(mm => mm.Currency).HasColumnName("PriceCurrency").HasMaxLength(3);
            m.WithOwner();
        });

        builder.Property(p => p.Stock).IsRequired();
        builder.Property(p => p.ReservedStock).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.OwnsMany(p => p.Reservations, r =>
        {
            r.ToTable("ProductReservations");
            r.WithOwner().HasForeignKey(x => x.ProductId);
            r.HasKey(x => x.Id);
            // Ids are client-generated in the domain ctor. Without this, EF treats
            // newly added reservations as MODIFIED (existing) instead of ADDED —
            // dotnet/efcore#27736 — and SaveChanges throws "does not exist".
            r.Property(x => x.Id).ValueGeneratedNever();
            r.Property(x => x.Quantity).IsRequired();
            r.Property(x => x.CreatedAt).IsRequired();
            r.Property(x => x.ExpiresAt).IsRequired();
        });

        builder.Ignore(p => p.DomainEvents);
        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
