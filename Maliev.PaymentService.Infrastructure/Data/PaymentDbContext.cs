using Maliev.PaymentService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Infrastructure.Data;

/// <summary>
/// Database context for the Payment Gateway Service.
/// Manages all payment-related entities using PostgreSQL 18.
/// </summary>
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Payment providers (Stripe, PayPal, Square, etc.).
    /// </summary>
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();

    /// <summary>
    /// Regional configurations for payment providers.
    /// </summary>
    public DbSet<ProviderConfiguration> ProviderConfigurations => Set<ProviderConfiguration>();

    /// <summary>
    /// Payment transactions processed through the gateway.
    /// </summary>
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    /// <summary>
    /// Immutable audit logs for payment transaction state changes.
    /// </summary>
    public DbSet<TransactionLog> TransactionLogs => Set<TransactionLog>();

    /// <summary>
    /// Webhook events received from payment providers.
    /// </summary>
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    /// <summary>
    /// Refund transactions for completed payments.
    /// </summary>
    public DbSet<RefundTransaction> RefundTransactions => Set<RefundTransaction>();

    /// <summary>
    /// Configures entity mappings and database conventions.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
