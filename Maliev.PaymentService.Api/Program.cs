using Maliev.PaymentService.Api.Authorization;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Maliev.Aspire.ServiceDefaults;


var builder = WebApplication.CreateBuilder(args);

// --- Secrets & Configuration ---
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// --- Infrastructure & Observability ---
builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
builder.AddStandardMiddleware(options =>
{
    options.EnableRequestLogging = true;
});
builder.AddServiceMeters("payments-meter"); // Register service meters for OpenTelemetry business metrics

builder.AddRedisDistributedCache(instanceName: "payment:"); // Redis with in-memory fallback
builder.AddRedisConnectionMultiplexer(); // Register IConnectionMultiplexer for IdempotencyService
builder.AddMassTransitWithRabbitMq(); // RabbitMQ message bus (non-blocking startup)
builder.AddPostgresDbContext<PaymentDbContext>(
    connectionName: "PaymentDbContext",
    enableDynamicJson: true); // Enable dynamic JSON for polymorphic payment provider data

builder.AddIAMServiceClient(); // IAM service client for permission registration

// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

// Permission-based Authorization
builder.Services.AddPermissionAuthorization();

builder.Services.AddIAMRegistration<PaymentIAMRegistrationService>();

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.AddStandardOpenApi(
        title: "MALIEV Payment Gateway Service API",
        description: "Payment processing gateway service. Handles payment initiation with idempotency keys, multi-provider support, payment status tracking, full and partial refund processing, and webhook endpoints for provider callbacks.");
}

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Register metrics service
builder.Services.AddSingleton<Maliev.PaymentService.Core.Interfaces.IMetricsService, Maliev.PaymentService.Infrastructure.Metrics.PrometheusMetricsService>();

// Configure Data Protection for credential encryption
builder.Services.AddDataProtection();

// Register circuit breaker state manager
builder.Services.AddSingleton<Maliev.PaymentService.Infrastructure.Resilience.CircuitBreakerStateManager>();

// Register encryption service
builder.Services.AddScoped<Maliev.PaymentService.Infrastructure.Encryption.IEncryptionService, Maliev.PaymentService.Infrastructure.Encryption.CredentialEncryptionService>();

// Register repositories
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IProviderRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.ProviderRepository>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.PaymentRepository>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IRefundRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.RefundRepository>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.WebhookRepository>();

// Register HttpClient for provider adapters with resilience
builder.Services.AddHttpClient("PaymentProviders")
    .AddStandardResilienceHandler();

// Register provider factory
builder.Services.AddScoped<Maliev.PaymentService.Infrastructure.Providers.ProviderFactory>();

// Register services
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IProviderManagementService, Maliev.PaymentService.Infrastructure.Services.ProviderManagementService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentRoutingService, Maliev.PaymentService.Infrastructure.Services.PaymentRoutingService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentService, Maliev.PaymentService.Infrastructure.Services.PaymentService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentStatusService, Maliev.PaymentService.Infrastructure.Services.PaymentStatusService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IRefundService, Maliev.PaymentService.Infrastructure.Services.RefundService>();

// Register webhook services
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookValidationService, Maliev.PaymentService.Infrastructure.Services.WebhookValidationService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookProcessingService, Maliev.PaymentService.Infrastructure.Services.WebhookProcessingService>();
builder.Services.AddHostedService<Maliev.PaymentService.Infrastructure.Services.WebhookCleanupService>();

// Register idempotency service (uses Redis from AddRedisDistributedCache)
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.RedisIdempotencyService>();

// Register event publisher
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IEventPublisher, Maliev.PaymentService.Infrastructure.Messaging.MassTransitEventPublisher>();

builder.Services.AddControllers();

var app = builder.Build();

// Force instantiation of metrics service to ensure OpenTelemetry meters are created
var metricsService = app.Services.GetRequiredService<Maliev.PaymentService.Core.Interfaces.IMetricsService>();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Run database migrations on startup
await app.MigrateDatabaseAsync<PaymentDbContext>();

// Seed test payment provider for development/testing
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    if (!await dbContext.PaymentProviders.AnyAsync())
    {
        var testProvider = new Maliev.PaymentService.Core.Entities.PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = "stripe",
            DisplayName = "Stripe (Test)",
            Status = Maliev.PaymentService.Core.Enums.ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "THB", "USD", "EUR" },
            Priority = 1,
            Credentials = new Dictionary<string, string>
            {
                { "ApiKey", "sk_test_development_key" }
            },
            Configurations = new List<Maliev.PaymentService.Core.Entities.ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.PaymentProviders.Add(testProvider);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded test payment provider: {ProviderName}", testProvider.Name);
    }
}

// Middleware Pipeline
app.UseStandardMiddleware();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints after middleware
app.MapControllers();

// Map Aspire default endpoints (/health, /alive, /metrics)
app.MapDefaultEndpoints(servicePrefix: "payment");

// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "payment");

Log.ServiceStarted(logger);
await app.RunAsync();

/// <summary>
/// Main program class for the Payment Service API.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "PaymentService started successfully")]
        public static partial void ServiceStarted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed - application may not function correctly")]
        public static partial void MigrationFailed(ILogger logger, Exception exception);
    }
}

