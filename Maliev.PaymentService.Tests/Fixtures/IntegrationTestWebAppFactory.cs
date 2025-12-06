using Maliev.PaymentService.Core.Constants;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;

namespace Maliev.PaymentService.Tests.Fixtures;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RSA _testRsa;
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";

    public IntegrationTestWebAppFactory()
    {
        // Disable default claim mapping globally
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        // Generate ephemeral RSA key for test JWT tokens
        _testRsa = RSA.Create(2048);

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithDatabase("payment_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        // Start container synchronously to ensure it's ready before ConfigureWebHost is called
        _postgresContainer.StartAsync().GetAwaiter().GetResult();

        // Export the test RSA public key as PEM and Base-64 encode it
        var publicKeyPem = _testRsa.ExportRSAPublicKeyPem();
        var publicKeyBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(publicKeyPem));

        // Set environment variables immediately after container starts
        // These must be set BEFORE WebHost building begins
        Environment.SetEnvironmentVariable("ConnectionStrings__PaymentDbContext", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("RabbitMq__Enabled", "false");
        Environment.SetEnvironmentVariable("RabbitMQ__Enabled", "false");
        Environment.SetEnvironmentVariable("Redis__Enabled", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__PublicKey", publicKeyBase64);
    }

    public Task InitializeAsync()
    {
        // Container is already started in constructor
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        _testRsa.Dispose();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Add in-memory distributed cache for tests (replaces Redis)
            services.AddDistributedMemoryCache();

            // Replace RedisIdempotencyService with in-memory TestIdempotencyService
            services.RemoveAll<IIdempotencyService>();
            services.AddSingleton<IIdempotencyService, TestIdempotencyService>();

            // Replace MassTransit EventPublisher with in-memory TestEventPublisher
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher, TestEventPublisher>();

            // Remove existing DbContext registration
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();
            services.RemoveAll<PaymentDbContext>();

            // Add DbContext with Testcontainers connection string
            services.AddDbContext<PaymentDbContext>(options =>
            {
                var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(_postgresContainer.GetConnectionString());
                dataSourceBuilder.EnableDynamicJson();
                var dataSource = dataSourceBuilder.Build();
                options.UseNpgsql(dataSource);
                // Suppress PendingModelChangesWarning for tests
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // PostConfigure JWT Bearer options to use our test RSA key
            services.PostConfigureAll<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(options =>
            {
                options.MapInboundClaims = false; // Disable default mapping to preserve custom claims
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestIssuer,
                    ValidAudience = TestAudience,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(_testRsa),
                    ClockSkew = TimeSpan.Zero // No clock skew for tests
                };
            });

            // Build service provider and use EnsureCreated for tests (simpler than migrations)
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Creates a test JWT token with specified claims for integration testing.
    /// </summary>
    /// <param name="userId">User ID claim</param>
    /// <param name="roles">User roles</param>
    /// <param name="additionalClaims">Additional claims to include</param>
    /// <returns>JWT token string</returns>
    public string CreateTestJwtToken(string userId = "test-user", string[]? roles = null, Dictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // Add required service identity claims using constants
            new(AuthConstants.ClaimTypes.ServiceId, "test-service"),
            new(AuthConstants.ClaimTypes.ServiceName, "Test Service")
        };

        // Add roles
        roles ??= new[] { "Admin" };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add additional claims
        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var credentials = new SigningCredentials(
            new RsaSecurityKey(_testRsa),
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
