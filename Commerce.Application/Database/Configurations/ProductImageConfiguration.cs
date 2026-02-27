using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(pi => pi.ProductId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(pi => pi.ImageUrl)
            .IsRequired()
            .HasMaxLength(2048); // S3 URL or local path

        builder.Property(pi => pi.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(pi => pi.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(pi => pi.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // ── Global Query Filter (soft-delete) ─────────────────────────
        builder.HasQueryFilter(pi => !pi.Product.IsDeleted);

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(pi => new { pi.ProductId, pi.DisplayOrder })
            .HasDatabaseName("IX_ProductImage_ProductId");
    }
}