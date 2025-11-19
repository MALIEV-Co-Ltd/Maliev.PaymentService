using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for PaymentProvider entity.
/// Maps to payment_providers table with indexes and constraints.
/// </summary>
public class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.ToTable("payment_providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.SupportedCurrencies)
            .HasColumnName("supported_currencies")
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .IsRequired();

        builder.Property(p => p.Priority)
            .HasColumnName("priority")
            .IsRequired();

        builder.Property(p => p.Credentials)
            .HasColumnName("credentials")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        // Indexes
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("ix_payment_providers_name");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_payment_providers_status");

        builder.HasIndex(p => p.Priority)
            .HasDatabaseName("ix_payment_providers_priority");

        builder.HasIndex(p => p.DeletedAt)
            .HasDatabaseName("ix_payment_providers_deleted_at");

        // Relationships
        builder.HasMany(p => p.Configurations)
            .WithOne(c => c.PaymentProvider)
            .HasForeignKey(c => c.PaymentProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Query filter for soft delete
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
