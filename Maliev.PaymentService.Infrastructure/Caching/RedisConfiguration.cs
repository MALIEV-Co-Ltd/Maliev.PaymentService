using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Maliev.PaymentService.Infrastructure.Caching;

/// <summary>
/// Redis configuration for distributed caching and idempotency.
/// </summary>
public static class RedisConfiguration
{
    /// <summary>
    /// Configures StackExchange.Redis ConnectionMultiplexer and distributed cache.
    /// </summary>
    public static IServiceCollection AddRedisConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Support both Redis:Host (from Google Secret Manager) and Redis:Configuration (from appsettings)
        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? configuration["Redis:Host"]
            ?? configuration["Redis:Configuration"]
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ConnectTimeout = 5000;
            configOptions.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Add IDistributedCache for caching (used by PaymentStatusService)
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "payment_gateway:";
        });

        return services;
    }
}
