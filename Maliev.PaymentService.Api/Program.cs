using FluentValidation;
using Maliev.PaymentService.Api.Middleware;
using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Prometheus;
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

    // Use Serilog for logging
    builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

// Configure OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1");

// Configure FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure DbContext with PostgreSQL
var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("PaymentDatabase")!);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(dataSource));

// Register metrics service
builder.Services.AddSingleton<Maliev.PaymentService.Core.Interfaces.IMetricsService, Maliev.PaymentService.Infrastructure.Metrics.PrometheusMetricsService>();

// Configure MassTransit with RabbitMQ
Maliev.PaymentService.Infrastructure.Messaging.MassTransitConfiguration.AddMassTransitWithRabbitMQ(builder.Services, builder.Configuration);

// Register event publisher
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IEventPublisher, Maliev.PaymentService.Infrastructure.Messaging.MassTransitEventPublisher>();

// Configure Redis
Maliev.PaymentService.Infrastructure.Caching.RedisConfiguration.AddRedisConfiguration(builder.Services, builder.Configuration);

// Register idempotency service
builder.Services.AddScoped<Maliev.PaymentService.Core.Interfaces.IIdempotencyService, Maliev.PaymentService.Infrastructure.Caching.RedisIdempotencyService>();

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

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["JwtAuthentication:Authority"];
        options.Audience = builder.Configuration["JwtAuthentication:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("JwtAuthentication:RequireHttpsMetadata");
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Configure health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("PaymentDatabase")!,
        name: "postgresql",
        tags: new[] { "db", "ready" })
    .AddRedis(
        builder.Configuration["Redis:Configuration"]!,
        name: "redis",
        tags: new[] { "cache", "ready" })
    .AddRabbitMQ(
        sp =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var port) ? port : 5672,
                UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/"
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        },
        name: "rabbitmq",
        tags: new[] { "messaging", "ready" });

// TODO: Additional services will be configured in later tasks

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/payments/openapi/{documentName}.json");
}

// Configure middleware pipeline order: Correlation -> Exception -> Logging -> Auth
app.UseCorrelationIdMiddleware();
app.UseExceptionHandlingMiddleware();
app.UseRequestLoggingMiddleware();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseJwtAuthenticationMiddleware();
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
