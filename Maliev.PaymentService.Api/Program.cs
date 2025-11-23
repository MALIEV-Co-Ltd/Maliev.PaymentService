using FluentValidation;
using Maliev.PaymentService.Api.Middleware;
using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PaymentGatewayService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/payment-gateway-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting Payment Gateway Service");

    var builder = WebApplication.CreateBuilder(args);

    // Configuration is loaded from:
    // 1. appsettings.json / appsettings.{Environment}.json
    // 2. Environment variables (including secrets injected by External Secrets Operator in Kubernetes)
    Log.Information("Loading configuration from appsettings and environment variables");

    // Use Serilog for logging
    builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

// Configure OpenAPI with XML documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Payment Gateway Service API";
        document.Info.Version = "v1";
        document.Info.Description = "RESTful API for payment processing with multi-provider support, intelligent routing, and comprehensive monitoring";
        return Task.CompletedTask;
    });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.AddSchemaTransformer((schema, context, cancellationToken) =>
        {
            // XML documentation is automatically included via OpenAPI schema generation
            return Task.CompletedTask;
        });
    }
});

// Configure FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure DbContext with PostgreSQL
// Support both PaymentDbContext (from Google Secret Manager) and PaymentDatabase (from appsettings)
var connectionString = builder.Configuration.GetConnectionString("PaymentDbContext")
    ?? builder.Configuration.GetConnectionString("PaymentDatabase")
    ?? throw new InvalidOperationException("Database connection string not found. Expected 'ConnectionStrings:PaymentDbContext' or 'ConnectionStrings:PaymentDatabase'");

var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(dataSource));

// Register metrics service
builder.Services.AddSingleton<Maliev.PaymentService.Core.Interfaces.IMetricsService, Maliev.PaymentService.Infrastructure.Metrics.PrometheusMetricsService>();

// Configure MassTransit with RabbitMQ (conditional)
// Support both RabbitMQ (standard) and RabbitMq (from Google Secret Manager) key formats
var rabbitMqEnabled = builder.Configuration.GetValue<bool?>("RabbitMQ:Enabled")
    ?? builder.Configuration.GetValue<bool?>("RabbitMq:Enabled")
    ?? true;
if (rabbitMqEnabled)
{
    Maliev.PaymentService.Infrastructure.Messaging.MassTransitConfiguration.AddMassTransitWithRabbitMQ(builder.Services, builder.Configuration);
    builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IEventPublisher, Maliev.PaymentService.Infrastructure.Messaging.MassTransitEventPublisher>();
    Log.Information("RabbitMQ messaging enabled");
}
else
{
    // Register no-op event publisher for development
    builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IEventPublisher, Maliev.PaymentService.Infrastructure.Messaging.NoOpEventPublisher>();
    Log.Warning("RabbitMQ DISABLED - Using no-op event publisher (Development mode only)");
}

// Configure Redis (conditional)
var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled", true);
if (redisEnabled)
{
    Maliev.PaymentService.Infrastructure.Caching.RedisConfiguration.AddRedisConfiguration(builder.Services, builder.Configuration);
    builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.RedisIdempotencyService>();
    Log.Information("Redis caching enabled");
}
else
{
    // Register in-memory idempotency service for development
    builder.Services.AddSingleton<Maliev.PaymentService.Core.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.InMemoryIdempotencyService>();
    Log.Warning("Redis DISABLED - Using in-memory idempotency service (Development mode only)");
}

// Register circuit breaker state manager
builder.Services.AddSingleton<Maliev.PaymentService.Infrastructure.Resilience.CircuitBreakerStateManager>();

// Register Polly resilience pipeline
builder.Services.AddSingleton<Polly.ResiliencePipeline<System.Net.Http.HttpResponseMessage>>(sp =>
{
    var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    return Maliev.PaymentService.Infrastructure.Resilience.PollyPolicies.CreateCombinedPolicy(configuration);
});

// Configure Data Protection for credential encryption
builder.Services.AddDataProtection();

// Register encryption service
builder.Services.AddScoped<Maliev.PaymentService.Infrastructure.Encryption.IEncryptionService, Maliev.PaymentService.Infrastructure.Encryption.CredentialEncryptionService>();

// Register repositories
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IProviderRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.ProviderRepository>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.PaymentRepository>();

// Register HttpClient for provider adapters
builder.Services.AddHttpClient();

// Register provider factory
builder.Services.AddScoped<Maliev.PaymentService.Infrastructure.Providers.ProviderFactory>();

// Register services
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IProviderManagementService, Maliev.PaymentService.Infrastructure.Services.ProviderManagementService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentRoutingService, Maliev.PaymentService.Infrastructure.Services.PaymentRoutingService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentService, Maliev.PaymentService.Infrastructure.Services.PaymentService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IPaymentStatusService, Maliev.PaymentService.Infrastructure.Services.PaymentStatusService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IRefundService, Maliev.PaymentService.Infrastructure.Services.RefundService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IRefundRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.RefundRepository>();

// Register webhook services
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookRepository, Maliev.PaymentService.Infrastructure.Data.Repositories.WebhookRepository>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookValidationService, Maliev.PaymentService.Infrastructure.Services.WebhookValidationService>();
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IWebhookProcessingService, Maliev.PaymentService.Infrastructure.Services.WebhookProcessingService>();
builder.Services.AddHostedService<Maliev.PaymentService.Infrastructure.Services.WebhookCleanupService>();

// Configure JWT Authentication with RSA public key validation
var jwtPublicKeyBase64 = builder.Configuration["Jwt__PublicKey"];
if (!string.IsNullOrEmpty(jwtPublicKeyBase64))
{
    try
    {
        // Decode base64 public key
        var publicKeyPem = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(jwtPublicKeyBase64));

        // Create RSA security key from PEM
        var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var securityKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt__Issuer"],
                    ValidAudience = builder.Configuration["Jwt__Audience"],
                    IssuerSigningKey = securityKey,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
        Log.Information("JWT Authentication enabled with RSA public key validation");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to configure JWT authentication with public key");
        throw;
    }
}
else
{
    // Development mode: Allow anonymous access to all endpoints
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true) // Always allow
            .Build();
    });
    Log.Warning("JWT Authentication DISABLED - Anonymous access allowed (Development mode only)");
}

// Configure health checks
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgresql",
        tags: new[] { "db", "ready" });

// Add Redis health check if enabled
if (redisEnabled)
{
    var redisConnectionString = builder.Configuration["Redis:Host"]
        ?? builder.Configuration["Redis:Configuration"]
        ?? "localhost:6379";

    healthChecksBuilder.AddRedis(
        redisConnectionString,
        name: "redis",
        tags: new[] { "cache", "ready" });
}

// Add RabbitMQ health check if enabled
if (rabbitMqEnabled)
{
    healthChecksBuilder.AddRabbitMQ(
        sp =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = builder.Configuration["RabbitMQ:Host"] ?? builder.Configuration["RabbitMq:Host"] ?? "localhost",
                Port = int.TryParse(builder.Configuration["RabbitMQ:Port"] ?? builder.Configuration["RabbitMq:Port"], out var port) ? port : 5672,
                UserName = builder.Configuration["RabbitMQ:Username"] ?? builder.Configuration["RabbitMq:Username"] ?? "guest",
                Password = builder.Configuration["RabbitMQ:Password"] ?? builder.Configuration["RabbitMq:Password"] ?? "guest",
                VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? builder.Configuration["RabbitMq:VirtualHost"] ?? "/"
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        },
        name: "rabbitmq",
        tags: new[] { "messaging", "ready" });
}

// TODO: Additional services will be configured in later tasks

// Add service defaults for .NET Aspire
builder.AddServiceDefaults();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapOpenApi("/payments/openapi/{documentName}.json");
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Payment Gateway Service API")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// Map Scalar UI with custom path
app.MapGet("/payments/scalar/v1", () => Results.Redirect("/scalar/v1"))
    .WithName("PaymentsScalarRedirect")
    .ExcludeFromDescription();

// Configure middleware pipeline order: Correlation -> Exception -> Logging -> Auth
app.UseCorrelationIdMiddleware();
app.UseExceptionHandlingMiddleware();
app.UseRequestLoggingMiddleware();

app.UseHttpsRedirection();

// Apply authentication/authorization middleware
if (!string.IsNullOrEmpty(jwtPublicKeyBase64))
{
    app.UseAuthentication();
    app.UseJwtAuthenticationMiddleware();
}

// Always apply authorization middleware (uses fallback policy in dev mode)
app.UseAuthorization();

// Apply rate limiting to webhook endpoints
app.UseMiddleware<WebhookRateLimitingMiddleware>();

// Configure Prometheus metrics endpoint at /payments/metrics
app.MapMetrics("/payments/metrics");

// Configure health check endpoints
app.MapHealthChecks("/payments/liveness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Liveness - always returns healthy if service is running
});

app.MapHealthChecks("/payments/readiness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") // Readiness - checks PostgreSQL, Redis, RabbitMQ
});

app.MapControllers();

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for integration testing
public partial class Program { }
