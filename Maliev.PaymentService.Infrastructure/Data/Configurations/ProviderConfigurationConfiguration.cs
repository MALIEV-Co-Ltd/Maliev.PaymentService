using Maliev.PaymentService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PaymentService.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for ProviderConfiguration entity.
/// Maps to provider_configurations table with indexes and constraints.
/// </summary>
public class ProviderConfigurationConfiguration : IEntityTypeConfiguration<ProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<ProviderConfiguration> builder)
    {
        builder.ToTable("provider_configurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(c => c.PaymentProviderId)
            .HasColumnName("payment_provider_id")
            .IsRequired();

        builder.Property(c => c.Region)
            .HasColumnName("region")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ApiBaseUrl)
            .HasColumnName("api_base_url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(c => c.MaxRetries)
            .HasColumnName("max_retries")
            .HasDefaultValue(3)
            .IsRequired();

        builder.Property(c => c.TimeoutSeconds)
            .HasColumnName("timeout_seconds")
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.PaymentProviderId)
            .HasDatabaseName("ix_provider_configurations_payment_provider_id");

        builder.HasIndex(c => new { c.PaymentProviderId, c.Region })
            .IsUnique()
            .HasDatabaseName("ix_provider_configurations_provider_region");

        builder.HasIndex(c => c.IsActive)
            .HasDatabaseName("ix_provider_configurations_is_active");
    }
}
