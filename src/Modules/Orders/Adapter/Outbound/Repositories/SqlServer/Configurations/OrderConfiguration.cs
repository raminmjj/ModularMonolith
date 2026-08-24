using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Orders.Application.Domain.Orders;
using ModularMonolith.Modules.Orders.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.UserId).IsRequired();
        builder.Property(o => o.PlacedAt).IsRequired();
        builder.Property(o => o.Status)
               .HasConversion(s => s.Value, v => OrderStatus.Parse(v))
               .HasMaxLength(20).IsRequired();
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Metadata.FindNavigation(nameof(Order.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(o => o.DomainEvents);
        builder.Property(o => o.RowVersion).IsRowVersion();
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(l => l.Id);
        // Ids are client-generated in the domain ctor. Without this, EF treats
        // newly added lines as MODIFIED (existing) instead of ADDED —
        // dotnet/efcore#27736 — and SaveChanges throws "does not exist".
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.ProductId).IsRequired();
        builder.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.ReservationId);
        builder.Ignore(l => l.LineTotal);
        builder.Ignore(l => l.DomainEvents);
    }
}
