using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for WebhookEvent entity.
/// </summary>
public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");

        builder.HasKey(e => e.Id)
            .HasName("pk_webhook_events");

        // Provider event ID unique per provider
        builder.HasIndex(e => new { e.ProviderId, e.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("uk_webhook_events_provider_event");

        // Index for finding webhooks by payment transaction
        builder.HasIndex(e => e.PaymentTransactionId)
            .HasDatabaseName("idx_webhook_events_payment_id")
            .HasFilter("payment_transaction_id IS NOT NULL");

        // Index for finding webhooks by refund transaction
        builder.HasIndex(e => e.RefundTransactionId)
            .HasDatabaseName("idx_webhook_events_refund_id")
            .HasFilter("refund_transaction_id IS NOT NULL");

        // Index for processing status queries
        builder.HasIndex(e => new { e.ProcessingStatus, e.CreatedAt })
            .HasDatabaseName("idx_webhook_events_processing_status")
            .IsDescending(false, true);

        // Index for webhook history queries
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_webhook_events_created_at")
            .IsDescending();

        // Index for retry processing
        builder.HasIndex(e => e.NextRetryAt)
            .HasDatabaseName("idx_webhook_events_next_retry")
            .HasFilter("next_retry_at IS NOT NULL");

        // Properties
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.ProviderId)
            .HasColumnName("provider_id")
            .IsRequired();

        builder.Property(e => e.ProviderEventId)
            .HasColumnName("provider_event_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PaymentTransactionId)
            .HasColumnName("payment_transaction_id");

        builder.Property(e => e.RefundTransactionId)
            .HasColumnName("refund_transaction_id");

        builder.Property(e => e.RawPayload)
            .HasColumnName("raw_payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.ParsedPayload)
            .HasColumnName("parsed_payload")
            .HasColumnType("jsonb");

        builder.Property(e => e.Signature)
            .HasColumnName("signature")
            .HasMaxLength(500);

        builder.Property(e => e.SignatureValidated)
            .HasColumnName("signature_validated")
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(255);

        builder.Property(e => e.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.ProcessingAttempts)
            .HasColumnName("processing_attempts")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamptz");

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("timestamptz");

        builder.Property(e => e.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(500);

        builder.Property(e => e.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType("timestamptz");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .HasDefaultValueSql("'\\x00'::bytea");  // Default empty bytea for PostgreSQL

        // Foreign keys
        builder.HasOne(e => e.PaymentProvider)
            .WithMany()
            .HasForeignKey(e => e.ProviderId)
            .HasConstraintName("fk_webhook_events_payment_providers")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PaymentTransaction)
            .WithMany()
            .HasForeignKey(e => e.PaymentTransactionId)
            .HasConstraintName("fk_webhook_events_payment_transactions")
            .OnDelete(DeleteBehavior.Restrict);

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_webhook_events_processing_status",
                "processing_status IN ('Pending', 'Processing', 'Completed', 'Failed', 'Duplicate')");
            t.HasCheckConstraint("chk_webhook_events_attempts",
                "processing_attempts >= 0");
        });
    }
}
