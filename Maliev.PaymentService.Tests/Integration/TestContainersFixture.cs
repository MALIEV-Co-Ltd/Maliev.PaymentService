using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Maliev.PaymentService.Tests.Integration;

/// <summary>
/// Testcontainers fixture for integration tests with real infrastructure.
/// Provides PostgreSQL 18, RabbitMQ 7.0, and Redis 7.2 containers.
/// </summary>
public class TestContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
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

        // RabbitMQ 7.0 container for message queue tests
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
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
    /// RabbitMQ connection string for message queue integration tests.
    /// </summary>
    public string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();

    /// <summary>
    /// Redis connection string for caching and idempotency integration tests.
    /// </summary>
    public string RedisConnectionString => _redisContainer.GetConnectionString();

    /// <summary>
    /// Initializes all containers asynchronously before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start all containers in parallel for faster test setup
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _rabbitMqContainer.StartAsync(),
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
            _rabbitMqContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask()
        );
    }
}
