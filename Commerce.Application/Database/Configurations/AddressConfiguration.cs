using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasColumnType("uuid");

        builder.Property(a => a.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.PhoneNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(a => a.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Governorate)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Area)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Street)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.BuildingNumber)
            .IsRequired(false)
            .HasMaxLength(20);

        builder.Property(a => a.Floor)
            .IsRequired(false)
            .HasMaxLength(20);

        builder.Property(a => a.Apartment)
            .IsRequired(false)
            .HasMaxLength(20);

        builder.Property(a => a.AddressName)
            .IsRequired(false)
            .HasMaxLength(255);

        builder.Property(a => a.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);
            
        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_Address_UserId");

        // ── Relationships ─────────────────────────────────────────────
        // Configured from UserConfiguration (HasMany → WithOne)
    }
}