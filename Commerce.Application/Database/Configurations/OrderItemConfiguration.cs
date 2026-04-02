using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(oi => oi.OrderId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(oi => oi.ProductId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.UnitPrice)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_OrderItem_Quantity", "\"Quantity\" > 0"));

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(oi => oi.OrderId)
            .HasDatabaseName("IX_OrderItem_OrderId");

        builder.HasIndex(oi => oi.ProductId)
            .HasDatabaseName("IX_OrderItem_ProductId");

        // ── Relationships ─────────────────────────────────────────────
        builder.HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .IsRequired(false) // Allow loading OrderItem without Product if Product is soft-deleted
            .OnDelete(DeleteBehavior.Restrict);

        // Configured from OrderConfiguration (HasMany → WithOne)
        // and ProductConfiguration (HasMany → WithOne)
    }
}