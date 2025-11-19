using Maliev.PaymentService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for TransactionLog entity.
/// Maps to transaction_logs table with immutable audit trail (no row version).
/// </summary>
public class TransactionLogConfiguration : IEntityTypeConfiguration<TransactionLog>
{
    public void Configure(EntityTypeBuilder<TransactionLog> builder)
    {
        builder.ToTable("transaction_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(l => l.PaymentTransactionId)
            .HasColumnName("payment_transaction_id")
            .IsRequired();

        builder.Property(l => l.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string?>()
            .HasMaxLength(50);

        builder.Property(l => l.NewStatus)
            .HasColumnName("new_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Message)
            .HasColumnName("message")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(l => l.ProviderResponse)
            .HasColumnName("provider_response")
            .HasColumnType("jsonb");

        builder.Property(l => l.ErrorDetails)
            .HasColumnName("error_details")
            .HasMaxLength(2000);

        builder.Property(l => l.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(l => l.PaymentTransactionId)
            .HasDatabaseName("ix_transaction_logs_payment_transaction_id");

        builder.HasIndex(l => l.EventType)
            .HasDatabaseName("ix_transaction_logs_event_type");

        builder.HasIndex(l => l.CreatedAt)
            .HasDatabaseName("ix_transaction_logs_created_at");

        builder.HasIndex(l => l.CorrelationId)
            .HasDatabaseName("ix_transaction_logs_correlation_id");

        // No row version - immutable logs
    }
}
