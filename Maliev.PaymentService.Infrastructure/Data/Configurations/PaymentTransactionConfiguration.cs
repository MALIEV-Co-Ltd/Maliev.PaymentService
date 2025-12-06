using Maliev.PaymentService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for PaymentTransaction entity.
/// Maps to payment_transactions table with indexes, constraints, and optimistic concurrency.
/// </summary>
public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.OrderId)
            .HasColumnName("order_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.PaymentProviderId)
            .HasColumnName("payment_provider_id")
            .IsRequired();

        builder.Property(p => p.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ProviderTransactionId)
            .HasColumnName("provider_transaction_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.PaymentUrl)
            .HasColumnName("payment_url")
            .HasMaxLength(1000);

        builder.Property(p => p.ReturnUrl)
            .HasColumnName("return_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.CancelUrl)
            .HasColumnName("cancel_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(p => p.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        builder.Property(p => p.ProviderErrorCode)
            .HasColumnName("provider_error_code")
            .HasMaxLength(100);

        builder.Property(p => p.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(p => p.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(p => p.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .HasDefaultValueSql("'\\x00'::bytea");  // Default empty bytea for PostgreSQL

        // Indexes
        builder.HasIndex(p => p.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_payment_transactions_idempotency_key");

        builder.HasIndex(p => p.OrderId)
            .HasDatabaseName("ix_payment_transactions_order_id");

        builder.HasIndex(p => p.CustomerId)
            .HasDatabaseName("ix_payment_transactions_customer_id");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_payment_transactions_status");

        builder.HasIndex(p => p.PaymentProviderId)
            .HasDatabaseName("ix_payment_transactions_payment_provider_id");

        builder.HasIndex(p => p.ProviderTransactionId)
            .HasDatabaseName("ix_payment_transactions_provider_transaction_id");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_payment_transactions_created_at");

        builder.HasIndex(p => p.CorrelationId)
            .HasDatabaseName("ix_payment_transactions_correlation_id");

        // Relationships
        builder.HasOne(p => p.PaymentProvider)
            .WithMany()
            .HasForeignKey(p => p.PaymentProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.TransactionLogs)
            .WithOne(l => l.PaymentTransaction)
            .HasForeignKey(l => l.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
