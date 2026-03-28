using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasColumnType("numeric(18,2)");      // > 0, max 2 decimals — validated in FluentValidation

        builder.Property(p => p.StockQuantity)
            .IsRequired()
            .HasDefaultValue(0);                  // >= 0 enforced at application level
        
        // shadow property for Optimistic Concurrency Token without manual handling
        builder.Property<uint>("Version")   // uint maps to PostgreSQL xid
            .HasColumnName("xmin")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(p => p.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        builder.OwnsMany(p => p.Specifications, sa =>
        {
            sa.ToJson();
            sa.Property(s => s.Key).IsRequired().HasMaxLength(50);
            sa.Property(s => s.Value).IsRequired().HasMaxLength(50);
        });
        
        builder.Property(p => p.AverageRating)
            .IsRequired(false)
            .HasColumnType("numeric(3,2)");

        builder.Property(p => p.RatingCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.DeletedAt)
            .IsRequired(false);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // ── Global Query Filter (soft-delete) ─────────────────────────
        builder.HasQueryFilter(p => !p.IsDeleted);

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(p => p.Category)
            .HasDatabaseName("IX_Product_Category");

        // Partial index — only active products
        builder.HasIndex(p => p.Category)
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_Product_Active_Category");

        builder.HasIndex(p => p.AverageRating)
            .IsDescending()
            .HasDatabaseName("IX_Product_AverageRating");

        // ── Relationships ─────────────────────────────────────────────
        builder.HasMany(p => p.Images)
            .WithOne(pi => pi.Product)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(p => p.Ratings)
            .WithOne(r => r.Product)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}