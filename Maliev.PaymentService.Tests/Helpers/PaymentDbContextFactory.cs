using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Tests.Helpers;

/// <summary>
/// Factory for creating PaymentDbContext instances for integration tests.
/// Uses Testcontainers connection strings for real PostgreSQL 18 database.
/// </summary>
public static class PaymentDbContextFactory
{
    /// <summary>
    /// Creates a new PaymentDbContext with Testcontainers PostgreSQL connection.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string from Testcontainers</param>
    /// <returns>Configured PaymentDbContext instance</returns>
    public static PaymentDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString)
            .EnableSensitiveDataLogging() // Enable for test debugging
            .EnableDetailedErrors()       // Enable for test debugging
            .Options;

        return new PaymentDbContext(options);
    }

    /// <summary>
    /// Creates a new PaymentDbContext and ensures the database is created and migrated.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string from Testcontainers</param>
    /// <returns>Configured and migrated PaymentDbContext instance</returns>
    public static async Task<PaymentDbContext> CreateAndMigrateAsync(string connectionString)
    {
        var context = Create(connectionString);
        await context.Database.MigrateAsync();
        return context;
    }
}
