using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(ci => ci.CartId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(ci => ci.ProductId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(ci => ci.Quantity)
            .IsRequired();

        builder.Property(ci => ci.UnitPriceSnapshot)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        // ── Global Query Filter (soft-delete) ─────────────────────────
        builder.HasQueryFilter(ci => !ci.Product.IsDeleted);

        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_CartItem_Quantity", "\"Quantity\" BETWEEN 1 AND 999"));

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(ci => ci.CartId)
            .HasDatabaseName("IX_CartItem_CartId");

        builder.HasIndex(ci => ci.ProductId)
            .HasDatabaseName("IX_CartItem_ProductId");

        // ── Relationships ─────────────────────────────────────────────
        // Configured from CartConfiguration (HasMany → WithOne)
        // and ProductConfiguration (HasMany → WithOne)
    }
}