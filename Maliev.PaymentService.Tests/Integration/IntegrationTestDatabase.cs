using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Tests.Integration;

/// <summary>
/// Prepares the disposable PostgreSQL schema used by integration tests.
/// </summary>
internal static class IntegrationTestDatabase
{
    /// <summary>
    /// Creates the schema and supplies deterministic values for legacy byte-array
    /// row-version columns that PostgreSQL does not generate automatically.
    /// </summary>
    public static async Task PrepareAsync(PaymentDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE payment_transactions ALTER COLUMN row_version SET DEFAULT '\x00'::bytea;
            ALTER TABLE refund_transactions ALTER COLUMN row_version SET DEFAULT '\x00'::bytea;
            ALTER TABLE webhook_events ALTER COLUMN row_version SET DEFAULT '\x00'::bytea;
            """);
    }
}
