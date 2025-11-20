using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PaymentService.Infrastructure.Messaging;

/// <summary>
/// MassTransit configuration for RabbitMQ message queue integration.
/// </summary>
public static class MassTransitConfiguration
{
    /// <summary>
    /// Configures MassTransit with RabbitMQ transport.
    /// </summary>
    public static IServiceCollection AddMassTransitWithRabbitMQ(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // Configure RabbitMQ transport
            x.UsingRabbitMq((context, cfg) =>
            {
                // Support both RabbitMQ (standard) and RabbitMq (from Google Secret Manager) key formats
                var rabbitMqHost = configuration["RabbitMQ:Host"] ?? configuration["RabbitMq:Host"] ?? "localhost";
                var rabbitMqPort = int.TryParse(configuration["RabbitMQ:Port"] ?? configuration["RabbitMq:Port"], out var port) ? port : 5672;
                var rabbitMqUsername = configuration["RabbitMQ:Username"] ?? configuration["RabbitMq:Username"] ?? "guest";
                var rabbitMqPassword = configuration["RabbitMQ:Password"] ?? configuration["RabbitMq:Password"] ?? "guest";
                var rabbitMqVirtualHost = configuration["RabbitMQ:VirtualHost"] ?? configuration["RabbitMq:VirtualHost"] ?? "/";

                cfg.Host(rabbitMqHost, (ushort)rabbitMqPort, rabbitMqVirtualHost, h =>
                {
                    h.Username(rabbitMqUsername);
                    h.Password(rabbitMqPassword);
                });

                // Configure message retry policy
                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(10),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                // Configure endpoint naming convention
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
