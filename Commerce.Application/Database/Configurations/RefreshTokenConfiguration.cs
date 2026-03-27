using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(rt => rt.UserId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(rt => rt.TokenHash)
            .IsRequired()
            .HasMaxLength(64);            // SHA-256 hex output is always 64 chars
        
        builder.Property(rt => rt.FamilyId)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(rt => rt.RevokedAt)
            .IsRequired(false);                   // nullable — null means active
        
        builder.Property(x => x.RevokedReason)
            .HasConversion<string>();

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_RefreshToken_UserId");

        builder.HasIndex(rt => rt.TokenHash)
            .HasDatabaseName("IX_RefreshToken_TokenHash");
        
        // Revoke-all-for-family: reuse/theft detection
        builder.HasIndex(rt => rt.FamilyId)
            .HasDatabaseName("IX_RefreshToken_FamilyId");

        // CleanupJob: find revoked tokens older than 30 days
        builder.HasIndex(rt => new { rt.RevokedAt, rt.CreatedAt })
            .HasDatabaseName("IX_RefreshToken_RevokedAt_CreatedAt");
    }
}