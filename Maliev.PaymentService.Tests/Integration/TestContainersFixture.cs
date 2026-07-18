using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Maliev.PaymentService.Tests.Integration;

/// <summary>
/// Testcontainers fixture for integration tests with real infrastructure.
/// Provides PostgreSQL 18, RabbitMQ 7.0, and Redis 7.2 containers.
/// </summary>
public class TestContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;

    public TestContainersFixture()
    {
        // PostgreSQL 18 container for database tests
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .WithDatabase("payment_gateway_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        // Redis 7.2 container for caching and idempotency tests
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// PostgreSQL connection string for database integration tests.
    /// </summary>
    public string PostgresConnectionString => _postgresContainer.GetConnectionString();

    /// <summary>
    /// Redis connection string for caching and idempotency integration tests.
    /// </summary>
    public string RedisConnectionString => _redisContainer.GetConnectionString();

    /// <summary>
    /// Redis endpoint in the format expected by StackExchange.Redis.
    /// </summary>
    public string RedisConfiguration =>
        $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}";

    /// <summary>
    /// Initializes all containers asynchronously before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start all containers in parallel for faster test setup
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync()
        );
    }

    /// <summary>
    /// Disposes all containers asynchronously after tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        // Stop and dispose all containers in parallel
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask()
        );
    }
}
