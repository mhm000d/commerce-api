using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(p => p.OrderId)
            .IsRequired()
            .HasColumnType("uuid");
        
        builder.Property(p => p.PaymentProviderId)
            .IsRequired()
            .HasMaxLength(255);
 
        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("numeric(18,2)");
 
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);
        
        builder.Property(p => p.PaymentMethod)
            .IsRequired()
            .HasMaxLength(100);
 
        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
 
        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Payment_Status",
                "\"Status\" IN ('Pending','Completed','Failed','Refunded')");
        });
        
        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(p => p.OrderId)
            .HasDatabaseName("IX_Payment_OrderId");
 
        // Stripe provider ID lookup for webhook idempotency
        builder.HasIndex(p => p.PaymentProviderId)
            .HasDatabaseName("IX_Payment_PaymentProviderId");
 
        // ── Relationships ─────────────────────────────────────────────
        // Configured from OrderConfiguration (HasOne → WithOne)
    }
}