using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Application.Database.Configurations;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");
 
        builder.HasKey(we => we.Id);
 
        builder.Property(we => we.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        
        builder.Property(we => we.EventId)
            .IsRequired()
            .HasMaxLength(255);
 
        builder.Property(we => we.EventType)
            .IsRequired()
            .HasMaxLength(255);
 
        // Full raw payload from Stripe — stored for auditability and replay
        builder.Property(we => we.Payload)
            .IsRequired()
            .HasColumnType("jsonb");
            
        builder.Property(we => we.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(100);
 
        builder.Property(we => we.ProcessedAt)
            .IsRequired(false);
 
        builder.Property(we => we.ErrorMessage)
            .IsRequired(false)
            .HasMaxLength(1000);
 
        builder.Property(we => we.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("NOW()");
            
        // ── Check Constraint ──────────────────────────────────────────
        builder.ToTable(t =>
            t.HasCheckConstraint(
                "CK_WebhookEvent_Status",
                "\"Status\" IN ('PENDING','PROCESSED','FAILED')"));
                
        // ── Indexes ──────────────────────────────────────────────────
        // Unique on Stripe EventId — core idempotency guard
        builder.HasIndex(we => we.EventId)
            .IsUnique()
            .HasDatabaseName("IX_WebhookEvent_EventId");
 
        // Status-based query for any retry/monitoring jobs
        builder.HasIndex(we => we.Status)
            .HasDatabaseName("IX_WebhookEvent_Status");
 
        // WebhookEvent has no FK relationships — it is a self-contained audit log.
    }
}