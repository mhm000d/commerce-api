using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(prt => prt.Id);

        builder.Property(prt => prt.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(prt => prt.UserId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(prt => prt.TokenHash)
            .IsRequired()
            .HasMaxLength(512); // SHA-256 hex string

        builder.Property(prt => prt.ExpiresAt)
            .IsRequired(); // CreatedAt + 1 hour (set in domain/service)

        builder.Property(prt => prt.UsedAt)
            .IsRequired(false); // nullable — null means unused

        builder.Property(prt => prt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(prt => prt.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_PasswordResetToken_Token");

        builder.HasIndex(prt => prt.UserId)
            .HasDatabaseName("IX_PasswordResetToken_UserId");
    }
}