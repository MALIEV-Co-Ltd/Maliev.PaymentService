using Maliev.PaymentService.Api.Middleware;
using Maliev.PaymentService.Infrastructure.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets & Configuration ---
builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

// --- Infrastructure & Observability ---
builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
builder.AddServiceMeters("payment-gateway"); // Register service meters for OpenTelemetry business metrics

builder.AddRedisDistributedCache(instanceName: "Payment:"); // Redis with in-memory fallback
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("redis") ?? "localhost:6379";
    return StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
});
builder.AddMassTransitWithRabbitMq(); // RabbitMQ message bus (non-blocking startup)
builder.AddPostgresDbContext<PaymentDbContext>(
    connectionStringName: "PaymentDbContext",
    enableDynamicJson: true); // Enable dynamic JSON for polymorphic payment provider data

// --- API Configuration ---
builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

// JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
builder.AddJwtAuthentication();

// Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
if (!builder.Environment.IsProduction())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi("v1", options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "MALIEV Payment Gateway Service API";
            document.Info.Version = "v1";
            document.Info.Description = "Payment processing gateway service. Handles payment initiation with idempotency keys, multi-provider support, payment status tracking, full and partial refund processing, and webhook endpoints for provider callbacks.";
            return Task.CompletedTask;
        });
    });
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

// Run database migrations on startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await app.MigrateDatabaseAsync<PaymentDbContext>();
    }
    catch (Exception ex)
    {
        Log.MigrationFailed(logger, ex);
        // Don't throw - allow app to start for debugging
    }
}

// Middleware Pipeline
app.UseCorrelationIdMiddleware();
app.UseExceptionHandlingMiddleware();
app.UseRequestLoggingMiddleware();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints after middleware
app.MapControllers();

// Map Aspire default endpoints (/health, /alive, /metrics)
app.MapDefaultEndpoints(servicePrefix: "payments");

// Map OpenAPI and Scalar documentation (dev/staging only)
app.MapApiDocumentation(servicePrefix: "payments");

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

