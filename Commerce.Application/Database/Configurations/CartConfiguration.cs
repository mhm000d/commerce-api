using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // ── Indexes ──────────────────────────────────────────────────
        // One cart per user
        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Cart_UserId");

        // ── Relationships ─────────────────────────────────────────────
        builder.HasMany(c => c.Items)
            .WithOne(ci => ci.Cart)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // HasOne(User) configured from UserConfiguration
    }
}