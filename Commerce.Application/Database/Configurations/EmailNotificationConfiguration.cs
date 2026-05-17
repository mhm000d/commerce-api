using System.Text.Json;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> builder)
    {
        builder.ToTable("EmailNotifications");
 
        builder.HasKey(en => en.Id);
 
        builder.Property(en => en.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();
 
        builder.Property(en => en.RecipientEmail)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(en => en.Template)
            .IsRequired()
            .HasConversion<string>()              // "ORDER_CONFIRMATION" | "PASSWORD_RESET"
            .HasMaxLength(50);
 
        // Dynamic template variables serialized to JSON
        // builder.Property(en => en.TemplateData)
        //     .IsRequired()
        //     .HasColumnType("jsonb");
        
        // builder.OwnsOne(e => e.TemplateData, t =>
        // {
        //     t.ToJson();
        // });
        // FIX: Dictionary<string,string> → JSONB via explicit converter.
        // OwnsOne().ToJson() is for owned entity types, not plain dictionaries.
        // builder.Property(en => en.TemplateData)
        //     .IsRequired()
        //     .HasColumnType("jsonb")
        //     .HasConversion(
        //         v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
        //         v => JsonSerializer.Deserialize<Dictionary<string, string>>(
        //             v, JsonSerializerOptions.Default) ?? new Dictionary<string, string>()
        //     );
        
        // EmailNotificationConfiguration.cs — replace the TemplateData property config
        builder.Property(en => en.TemplateData)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(
                         v, JsonSerializerOptions.Default)
                     ?? new Dictionary<string, string>()
            )
            .Metadata.SetValueComparer(
                new ValueComparer<Dictionary<string, string>>(
                    // Equality: same keys and same values
                    (a, b) => a != null && b != null
                                        && a.Count == b.Count
                                        && a.All(kv => b.ContainsKey(kv.Key) && b[kv.Key] == kv.Value),
                    // Hash code: order-independent
                    d => d.Aggregate(0, (acc, kv) =>
                        HashCode.Combine(acc, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
                    // Snapshot: deep copy so EF stores the original for comparison
                    d => d.ToDictionary(kv => kv.Key, kv => kv.Value)
                )
            );
        
        builder.Property(en => en.Status)
            .IsRequired()
            .HasConversion<string>()              // "PENDING" | "SENT" | "FAILED" | "PERMANENTLY_FAILED"
            .HasMaxLength(50);
            
        builder.Property(en => en.Attempts)
            .IsRequired()
            .HasDefaultValue(0);
 
        builder.Property(en => en.MaxAttempts)
            .IsRequired()
            .HasDefaultValue(3);
 
        builder.Property(en => en.LastAttemptAt)
            .IsRequired(false);
 
        builder.Property(en => en.SentAt)
            .IsRequired(false);
 
        builder.Property(en => en.ErrorMessage)
            .IsRequired(false)
            .HasMaxLength(1000);
            
        builder.Property(en => en.OrderId)
            .IsRequired(false)
            .HasColumnType("uuid");
 
        builder.Property(en => en.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
        {
            // FIX: values must match what HasConversion<string>() produces from your enum.
            // EmailStatus.Pending → "Pending", not "Queued"
            t.HasCheckConstraint(
                "CK_EmailNotification_Status",
                "\"Status\" IN ('Pending','Sent','Failed','PermanentlyFailed')");

            t.HasCheckConstraint(
                "CK_EmailNotification_Attempts",
                "\"Attempts\" >= 0 AND \"Attempts\" <= \"MaxAttempts\"");
        });
        
        // ── Indexes ──────────────────────────────────────────────────
        // EmailSenderJob queries by Status every minute
        builder.HasIndex(en => en.Status)
            .HasDatabaseName("IX_EmailNotification_Status");
 
        builder.HasIndex(en => en.OrderId)
            .HasDatabaseName("IX_EmailNotification_OrderId");
 
        // ── Relationships ─────────────────────────────────────────────
        // Configured from OrderConfiguration (HasMany → WithOne)
    }
}