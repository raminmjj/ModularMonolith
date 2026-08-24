using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.CustomerId).IsRequired();
        builder.Property(t => t.OrderId).IsRequired();
        builder.HasIndex("CustomerId");

        builder.OwnsOne(t => t.Amount, m =>
        {
            m.Property(mm => mm.Amount).HasColumnName("Amount").HasPrecision(18, 2);
            m.Property(mm => mm.Currency).HasColumnName("Currency").HasMaxLength(3);
            m.WithOwner();
        });

        builder.Property(t => t.Status)
               .HasConversion(s => s.Value, v => PaymentStatus.Parse(v))
               .HasMaxLength(20).IsRequired();

        // Token snapshot stored as flat columns — the token is opaque, never a PAN.
        builder.OwnsOne(t => t.Method, m =>
        {
            m.Property(x => x.Token).HasColumnName("MethodToken").HasMaxLength(200).IsRequired();
            m.Property(x => x.CardType).HasColumnName("MethodCardType").HasMaxLength(20).IsRequired();
            m.Property(x => x.ExpiryDate).HasColumnName("MethodExpiryDate").IsRequired();
            m.WithOwner();
        });

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.FailedAt);
        builder.Property(t => t.FailureReason).HasMaxLength(500);
        builder.Ignore(t => t.DomainEvents);
        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
