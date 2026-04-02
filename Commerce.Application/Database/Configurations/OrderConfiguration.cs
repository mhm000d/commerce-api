using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(o => o.UserId)
            .IsRequired()
            .HasColumnType("uuid");

        // Human-readable order number, e.g. "Order #000421017"
        // Generated in the application layer (e.g. sequence or padded counter)
        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("IX_Order_OrderNumber");

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>() // stored as "PLACED", "PAID", etc.
            .HasMaxLength(20);

        builder.Property(o => o.TotalAmount)
            .IsRequired()
            .HasColumnType("numeric(18,2)");
        
        builder.OwnsOne(o => o.ShippingAddressSnapshot, sa =>
        {
            sa.ToJson();
        });

        builder.Property(o => o.ConfirmationEmailSent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(o => o.ConfirmationEmailSentAt)
            .IsRequired(false);

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Order_Status",
                "\"Status\" IN ('Placed','Paid','Shipped','Delivered','Cancelled')");
        });

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(o => new { o.UserId, o.CreatedAt })
            .IsDescending(false, true) // UserId ASC, CreatedAt DESC
            .HasDatabaseName("IX_Order_UserId_CreatedAt");

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_Order_Status");

        // ── Relationships ─────────────────────────────────────────────
        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Payment)
            .WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict); // payment must be settled before order deletion
        
        builder.HasMany(o => o.EmailNotifications)
            .WithOne(en => en.Order)
            .HasForeignKey(en => en.OrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}