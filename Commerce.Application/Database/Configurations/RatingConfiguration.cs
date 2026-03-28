using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");
 
        builder.HasKey(r => r.Id);
 
        builder.Property(r => r.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();
 
        builder.Property(r => r.ProductId)
            .IsRequired()
            .HasColumnType("uuid");
 
        builder.Property(r => r.UserId)
            .IsRequired()
            .HasColumnType("uuid");
 
        builder.Property(r => r.Score)
            .IsRequired();
        // 1–5 validated via FluentValidation / domain rule
        // DB-level: add a check constraint below
 
        builder.Property(r => r.Comment)
            .IsRequired(false)
            .HasMaxLength(200);
 
        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
 
        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_Rating_Score", "\"Score\" BETWEEN 1 AND 5"));
        
        // ── Global Query Filter ─────────────────────────
        // If product is hidden → rating is also hidden
        builder.HasQueryFilter(r => !r.Product.IsDeleted);
 
        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(r => r.ProductId)
            .HasDatabaseName("IX_Rating_ProductId");
 
        // One rating per user per product
        builder.HasIndex(r => new { r.UserId, r.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_Rating_UserId_ProductId");
 
        // ── Relationships ─────────────────────────────────────────────
        // Configured from UserConfiguration and ProductConfiguration
    }
}