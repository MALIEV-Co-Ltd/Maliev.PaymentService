using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for RefundTransaction entity.
/// </summary>
public class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("refund_transactions");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        // Foreign keys
        builder.Property(e => e.PaymentTransactionId)
            .HasColumnName("payment_transaction_id")
            .IsRequired();

        builder.Property(e => e.ProviderId)
            .HasColumnName("provider_id")
            .IsRequired();

        // Refund details
        builder.Property(e => e.ProviderRefundId)
            .HasColumnName("provider_refund_id")
            .HasMaxLength(255);

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v.ToString().ToLower(),
                v => Enum.Parse<RefundStatus>(v, ignoreCase: true))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500);

        builder.Property(e => e.RefundType)
            .HasColumnName("refund_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ProviderErrorCode)
            .HasColumnName("provider_error_code")
            .HasMaxLength(100);

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        builder.Property(e => e.InternalNotes)
            .HasColumnName("internal_notes")
            .HasMaxLength(2000);

        // Timestamps
        builder.Property(e => e.InitiatedAt)
            .HasColumnName("initiated_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .IsRequired();

        // Audit fields
        builder.Property(e => e.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(100);

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .HasDefaultValueSql("'\\x00'::bytea");  // Default empty bytea for PostgreSQL

        // Indexes
        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("uk_refund_transactions_idempotency_key");

        builder.HasIndex(e => new { e.PaymentTransactionId, e.CreatedAt })
            .HasDatabaseName("idx_refund_transactions_payment_id")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.Status, e.CreatedAt })
            .HasDatabaseName("idx_refund_transactions_status")
            .IsDescending(false, true);

        builder.HasIndex(e => e.ProviderId)
            .HasDatabaseName("idx_refund_transactions_provider_id");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("idx_refund_transactions_correlation_id");

        // Foreign key relationships
        builder.HasOne(e => e.PaymentTransaction)
            .WithMany()
            .HasForeignKey(e => e.PaymentTransactionId)
            .HasConstraintName("fk_refund_transactions_payment_transactions")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Provider)
            .WithMany()
            .HasForeignKey(e => e.ProviderId)
            .HasConstraintName("fk_refund_transactions_payment_providers")
            .OnDelete(DeleteBehavior.Restrict);

        // Check constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_refund_transactions_amount_positive", "amount > 0");
            t.HasCheckConstraint("chk_refund_transactions_currency_length", "LENGTH(currency) = 3");
            t.HasCheckConstraint("chk_refund_transactions_status",
                "status IN ('pending', 'processing', 'completed', 'failed')");
            t.HasCheckConstraint("chk_refund_transactions_type",
                "refund_type IN ('full', 'partial')");
        });
    }
}
