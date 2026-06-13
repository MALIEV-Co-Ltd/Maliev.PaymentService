using Maliev.Aspire.ServiceDefaults;
using Maliev.PaymentService.Api.Configuration;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Program.Log.StartingHost(bootstrapLogger, "Payment Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
    builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("payments-meter"); // Register service meters for OpenTelemetry business metrics

    builder.AddStandardCache("payment:"); // Redis + in-memory fallback, memory-optimized (includes IConnectionMultiplexer)
    builder.AddMassTransitWithRabbitMq(x =>
    {
        x.AddEntityFrameworkOutbox<PaymentDbContext>(options =>
        {
            _ = options.UsePostgres();
            options.UseBusOutbox();
        });

        x.AddConsumer<Maliev.PaymentService.Api.Consumers.OrderAcceptedEventConsumer>();
        x.AddConsumer<Maliev.PaymentService.Api.Consumers.InvoiceCreatedEventConsumer>();
    }); // RabbitMQ message bus (non-blocking startup)
    builder.AddPostgresDbContext<PaymentDbContext>(
        connectionName: "PaymentDbContext",
        enableDynamicJson: true); // Enable dynamic JSON for polymorphic payment provider data

    const string ServiceName = "payment";
    builder.AddIAMServiceClient(ServiceName);

    // IAM Registration Service
    builder.Services.AddIAMRegistration<PaymentIAMRegistrationService>(ServiceName);

    // --- API Configuration ---
    builder.AddStandardCors(); // CORS with fail-fast validation

    // JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
    builder.AddJwtAuthentication();

    // Permission-based Authorization
    builder.Services.AddPermissionAuthorization();

    // Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Payment Gateway Service API",
            description: "Payment processing gateway service. Handles payment initiation with idempotency keys, multi-provider support, payment status tracking, full and partial refund processing, and webhook endpoints for provider callbacks.");
    }

    // Rate Limiting
    builder.AddStandardRateLimiting(); // Memory-optimized for low-spec nodes
    // Register metrics service
    builder.Services.AddSingleton<Maliev.PaymentService.Application.Interfaces.IMetricsService, Maliev.PaymentService.Infrastructure.Metrics.PrometheusMetricsService>();

    // Configure Data Protection for credential encryption
    builder.Services.AddDataProtection();

    // Register circuit breaker state manager
    builder.Services.AddSingleton<Maliev.PaymentService.Infrastructure.Resilience.CircuitBreakerStateManager>();

    // Register encryption service
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IEncryptionService, Maliev.PaymentService.Infrastructure.Encryption.CredentialEncryptionService>();

    // Register repositories
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IProviderRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.ProviderRepository>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IPaymentRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.PaymentRepository>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IRefundRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.RefundRepository>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IWebhookRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.WebhookRepository>();

    // Register HttpClient for provider adapters with resilience
    builder.Services.AddHttpClient("PaymentProviders")
        .AddStandardResilienceHandler();

    // Register provider factory
    builder.Services.AddScoped<Maliev.PaymentService.Infrastructure.Providers.ProviderFactory>();

    // Register services
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IProviderManagementService, Maliev.PaymentService.Infrastructure.Services.ProviderManagementService>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IPaymentRoutingService, Maliev.PaymentService.Infrastructure.Services.PaymentRoutingService>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IPaymentService, Maliev.PaymentService.Infrastructure.Services.PaymentService>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IPaymentStatusService, Maliev.PaymentService.Infrastructure.Services.PaymentStatusService>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IRefundService, Maliev.PaymentService.Infrastructure.Services.RefundService>();

    // Register webhook services
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IWebhookValidationService, Maliev.PaymentService.Infrastructure.Services.WebhookValidationService>();
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IWebhookProcessingService, Maliev.PaymentService.Infrastructure.Services.WebhookProcessingService>();
    builder.Services.AddHostedService<Maliev.PaymentService.Infrastructure.Services.WebhookRetryService>();
    builder.Services.AddHostedService<Maliev.PaymentService.Infrastructure.Services.WebhookCleanupService>();

    // Register idempotency service. Redis is required for shared production
    // idempotency, while local/test hosts use the in-memory fallback.
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddSingleton<Maliev.PaymentService.Application.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.InMemoryIdempotencyService>();
    }
    else
    {
        builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.RedisIdempotencyService>();
    }

    // Register event publisher
    builder.Services.AddScoped<Maliev.PaymentService.Application.Interfaces.IEventPublisher, Maliev.PaymentService.Infrastructure.Messaging.MassTransitEventPublisher>();

    builder.Services.AddControllers();

    var app = builder.Build();

    // Force instantiation of metrics service to ensure OpenTelemetry meters are created
    var metricsService = app.Services.GetRequiredService<Maliev.PaymentService.Application.Interfaces.IMetricsService>();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    PaymentProviderConfigurationValidator.ValidateOmiseForEnvironment(app.Configuration, app.Environment.EnvironmentName);
    PaymentProviderConfigurationValidator.ValidateStripeForEnvironment(app.Configuration, app.Environment.EnvironmentName);

    // Run database migrations on startup
    await app.MigrateDatabaseAsync<PaymentDbContext>();

    // Seed local payment providers for development/testing.
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var encryptionService = scope.ServiceProvider.GetRequiredService<Maliev.PaymentService.Application.Interfaces.IEncryptionService>();
            var omiseSection = app.Configuration.GetSection("PaymentProviders:Omise");
            var stripeSection = app.Configuration.GetSection("PaymentProviders:Stripe");
            var now = DateTime.UtcNow;

            var retiredUnsupportedProviders = await dbContext.PaymentProviders
                .IgnoreQueryFilters()
                .Include(provider => provider.Configurations)
                .Where(provider => provider.Name.ToLower() == "paypal" && provider.DeletedAt == null)
                .ToListAsync();

            foreach (var provider in retiredUnsupportedProviders)
            {
                provider.Status = Maliev.PaymentService.Domain.Enums.ProviderStatus.Disabled;
                provider.DeletedAt = now;
                provider.UpdatedAt = now;

                foreach (var configuration in provider.Configurations)
                {
                    configuration.IsActive = false;
                    configuration.UpdatedAt = now;
                }
            }

            var omiseProvider = await dbContext.PaymentProviders
                .IgnoreQueryFilters()
                .Include(provider => provider.Configurations)
                .FirstOrDefaultAsync(provider => provider.Name.ToLower() == "omise");

            var configuredPublicKey = omiseSection["PublicKey"];
            var configuredSecretKey = omiseSection["SecretKey"];
            var configuredWebhookSecret = omiseSection["WebhookSecret"];
            var configuredApiBaseUrl = omiseSection["ApiBaseUrl"];
            var publicKey = string.IsNullOrWhiteSpace(configuredPublicKey) ? "pkey_test_development_omise_key" : configuredPublicKey;
            var secretKey = string.IsNullOrWhiteSpace(configuredSecretKey) ? "skey_test_development_omise_key" : configuredSecretKey;
            var webhookSecret = string.IsNullOrWhiteSpace(configuredWebhookSecret) ? "whsec_omise_development_secret" : configuredWebhookSecret;
            var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl) ? "https://api.omise.co" : configuredApiBaseUrl;

            if (omiseProvider is null)
            {
                var providerId = Guid.NewGuid();
                omiseProvider = new Maliev.PaymentService.Domain.Entities.PaymentProvider
                {
                    Id = providerId,
                    Name = "omise",
                    DisplayName = "Omise",
                    Status = Maliev.PaymentService.Domain.Enums.ProviderStatus.Active,
                    SupportedCurrencies = new List<string> { "THB" },
                    Priority = 1,
                    Credentials = new Dictionary<string, string>
                    {
                        { "PublicKey", encryptionService.Encrypt(publicKey) },
                        { "SecretKey", encryptionService.Encrypt(secretKey) },
                        { "WebhookSecret", encryptionService.Encrypt(webhookSecret) }
                    },
                    Configurations = new List<Maliev.PaymentService.Domain.Entities.ProviderConfiguration>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            PaymentProviderId = providerId,
                            Region = "thailand",
                            ApiBaseUrl = apiBaseUrl,
                            IsActive = true,
                            MaxRetries = 3,
                            TimeoutSeconds = 30,
                            CreatedAt = now,
                            UpdatedAt = now
                        }
                    },
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.PaymentProviders.Add(omiseProvider);
                logger.LogInformation("Seeded primary local payment provider: {ProviderName}", omiseProvider.Name);
            }
            else
            {
                omiseProvider.Status = Maliev.PaymentService.Domain.Enums.ProviderStatus.Active;
                omiseProvider.DeletedAt = null;
                omiseProvider.Priority = 1;
                omiseProvider.SupportedCurrencies = new List<string> { "THB" };
                omiseProvider.UpdatedAt = now;
                omiseProvider.Credentials["PublicKey"] = encryptionService.Encrypt(publicKey);
                omiseProvider.Credentials["SecretKey"] = encryptionService.Encrypt(secretKey);
                omiseProvider.Credentials["WebhookSecret"] = encryptionService.Encrypt(webhookSecret);

                var configuration = omiseProvider.Configurations.FirstOrDefault();
                if (configuration is null)
                {
                    omiseProvider.Configurations.Add(new Maliev.PaymentService.Domain.Entities.ProviderConfiguration
                    {
                        Id = Guid.NewGuid(),
                        PaymentProviderId = omiseProvider.Id,
                        Region = "thailand",
                        ApiBaseUrl = apiBaseUrl,
                        IsActive = true,
                        MaxRetries = 3,
                        TimeoutSeconds = 30,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                else
                {
                    configuration.Region = "thailand";
                    configuration.ApiBaseUrl = apiBaseUrl;
                    configuration.IsActive = true;
                    configuration.UpdatedAt = now;
                }
            }

            var stripeProvider = await dbContext.PaymentProviders
                .IgnoreQueryFilters()
                .Include(provider => provider.Configurations)
                .FirstOrDefaultAsync(provider => provider.Name.ToLower() == "stripe");

            var configuredStripeApiKey = stripeSection["ApiKey"];
            var configuredStripeWebhookSecret = stripeSection["WebhookSecret"];
            var configuredStripeApiBaseUrl = stripeSection["ApiBaseUrl"];
            var stripeApiKey = string.IsNullOrWhiteSpace(configuredStripeApiKey) ? "sk_test_development_stripe_key" : configuredStripeApiKey;
            var stripeWebhookSecret = string.IsNullOrWhiteSpace(configuredStripeWebhookSecret) ? "whsec_stripe_development_secret" : configuredStripeWebhookSecret;
            var stripeApiBaseUrl = string.IsNullOrWhiteSpace(configuredStripeApiBaseUrl) ? "https://api.stripe.com" : configuredStripeApiBaseUrl;

            if (stripeProvider is null)
            {
                var providerId = Guid.NewGuid();
                stripeProvider = new Maliev.PaymentService.Domain.Entities.PaymentProvider
                {
                    Id = providerId,
                    Name = "stripe",
                    DisplayName = "Stripe",
                    Status = Maliev.PaymentService.Domain.Enums.ProviderStatus.Active,
                    SupportedCurrencies = new List<string> { "THB", "USD" },
                    Priority = 2,
                    Credentials = new Dictionary<string, string>
                    {
                        { "ApiKey", encryptionService.Encrypt(stripeApiKey) },
                        { "WebhookSecret", encryptionService.Encrypt(stripeWebhookSecret) }
                    },
                    Configurations = new List<Maliev.PaymentService.Domain.Entities.ProviderConfiguration>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            PaymentProviderId = providerId,
                            Region = "global",
                            ApiBaseUrl = stripeApiBaseUrl,
                            IsActive = true,
                            MaxRetries = 3,
                            TimeoutSeconds = 30,
                            CreatedAt = now,
                            UpdatedAt = now
                        }
                    },
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.PaymentProviders.Add(stripeProvider);
                logger.LogInformation("Seeded local payment provider: {ProviderName}", stripeProvider.Name);
            }
            else
            {
                stripeProvider.Status = Maliev.PaymentService.Domain.Enums.ProviderStatus.Active;
                stripeProvider.DeletedAt = null;
                stripeProvider.Priority = 2;
                stripeProvider.SupportedCurrencies = new List<string> { "THB", "USD" };
                stripeProvider.UpdatedAt = now;
                stripeProvider.Credentials["ApiKey"] = encryptionService.Encrypt(stripeApiKey);
                stripeProvider.Credentials["WebhookSecret"] = encryptionService.Encrypt(stripeWebhookSecret);

                var configuration = stripeProvider.Configurations.FirstOrDefault();
                if (configuration is null)
                {
                    stripeProvider.Configurations.Add(new Maliev.PaymentService.Domain.Entities.ProviderConfiguration
                    {
                        Id = Guid.NewGuid(),
                        PaymentProviderId = stripeProvider.Id,
                        Region = "global",
                        ApiBaseUrl = stripeApiBaseUrl,
                        IsActive = true,
                        MaxRetries = 3,
                        TimeoutSeconds = 30,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                else
                {
                    configuration.Region = "global";
                    configuration.ApiBaseUrl = stripeApiBaseUrl;
                    configuration.IsActive = true;
                    configuration.UpdatedAt = now;
                }
            }

            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(
                ex,
                "Skipped local payment provider seed because provider records changed during startup.");
        }
    }

    // Middleware Pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseStandardMiddleware();
    app.UseRouting();
    app.UseCors();

    // Custom rate limiting for webhooks
    app.UseMiddleware<Maliev.PaymentService.Api.Middleware.WebhookRateLimitingMiddleware>();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Map endpoints after middleware
    app.MapControllers();

    // Map Aspire default endpoints (/health, /alive, /metrics)
    app.MapDefaultEndpoints(servicePrefix: "payment");

    // Map OpenAPI and Scalar documentation (dev/staging only)
    app.MapApiDocumentation(servicePrefix: "payment");

    Program.Log.ServiceStarted(logger, "Payment Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Program.Log.HostTerminated(bootstrapLogger, ex, "Payment Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main program class for the Payment Service API.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed - application may not function correctly")]
        public static partial void MigrationFailed(ILogger logger, Exception exception);
    }
}
