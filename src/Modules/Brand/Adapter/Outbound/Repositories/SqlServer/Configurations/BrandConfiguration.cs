using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Brand.Application.Domain.Brands;
using BrandAggregate = ModularMonolith.Modules.Brand.Application.Domain.Brands.Brand;
using ModularMonolith.Modules.Brand.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<BrandAggregate>
{
    public void Configure(EntityTypeBuilder<BrandAggregate> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();

        builder.Property(b => b.Slug)
               .HasConversion(s => s.Value, v => Slug.Create(v))
               .HasMaxLength(60).IsRequired();
        builder.HasIndex("Slug").IsUnique(); // EF10: no member access through converted props

        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.LogoUrl).HasMaxLength(500);
        builder.Property(b => b.CountryOfOrigin).HasMaxLength(2).IsRequired();
        builder.Property(b => b.Status)
               .HasConversion(
                   v => v.Value,
                   v => v == BrandStatus.Approved.Value ? BrandStatus.Approved
                       : v == BrandStatus.Rejected.Value ? BrandStatus.Rejected
                       : BrandStatus.PendingReview)
               .HasMaxLength(20).IsRequired();
        builder.Property(b => b.RejectionReason).HasMaxLength(500);
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.Ignore(b => b.DomainEvents);
        builder.Property(b => b.RowVersion).IsRowVersion();
    }
}
