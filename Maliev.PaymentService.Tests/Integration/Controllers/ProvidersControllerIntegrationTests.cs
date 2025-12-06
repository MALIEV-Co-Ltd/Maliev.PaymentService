using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Infrastructure.Data;
using Maliev.PaymentService.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.PaymentService.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for Providers API endpoints.
/// Tests provider CRUD operations with real database.
/// </summary>
public class ProvidersControllerIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>, IAsyncLifetime
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;
    private PaymentDbContext? _dbContext;

    public ProvidersControllerIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();

        // Set JWT authorization header
        var token = _factory.CreateTestJwtToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task InitializeAsync()
    {
        // Get DbContext
        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task RegisterProvider_WithValidData_ReturnsCreatedProvider()
    {
        // Arrange
        var request = new RegisterProviderRequest
        {
            Name = "Stripe",
            DisplayName = "Stripe Payments",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "EUR", "GBP" },
            Priority = 1,
            Credentials = new Dictionary<string, string>
            {
                { "ApiKey", "sk_test_123456" },
                { "WebhookSecret", "whsec_123456" }
            },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new()
                {
                    Region = "US",
                    ApiBaseUrl = "https://api.stripe.com",
                    IsActive = true,
                    MaxRetries = 3,
                    TimeoutSeconds = 30
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/payments/v1/providers", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var provider = await response.Content.ReadFromJsonAsync<ProviderResponse>();
        Assert.NotNull(provider);
        Assert.Equal("Stripe", provider.Name);
        Assert.Equal("Stripe Payments", provider.DisplayName);
        Assert.Equal(ProviderStatus.Active, provider.Status);
        Assert.Equal(3, provider.SupportedCurrencies.Count);
        Assert.Contains("USD", provider.SupportedCurrencies);
        Assert.Single(provider.Configurations);

        // Verify credentials are not exposed in response
        Assert.Null(provider.Credentials);
    }

    [Fact]
    public async Task GetAllProviders_ReturnsListOfProviders()
    {
        // Arrange - Register a provider first
        var registerRequest = new RegisterProviderRequest
        {
            Name = "PayPal",
            DisplayName = "PayPal",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "EUR" },
            Priority = 2,
            Credentials = new Dictionary<string, string> { { "ClientId", "test" }, { "ClientSecret", "secret" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://api.paypal.com", IsActive = true }
            }
        };
        await _client.PostAsJsonAsync("/payments/v1/providers", registerRequest);

        // Act
        var response = await _client.GetAsync("/payments/v1/providers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providers = await response.Content.ReadFromJsonAsync<List<ProviderSummary>>();
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);
        Assert.Contains(providers, p => p.Name == "PayPal");
    }

    [Fact]
    public async Task GetProviderById_WithExistingId_ReturnsProvider()
    {
        // Arrange - Register a provider
        var registerRequest = new RegisterProviderRequest
        {
            Name = "Square",
            DisplayName = "Square",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 3,
            Credentials = new Dictionary<string, string> { { "AccessToken", "test_token" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://connect.squareup.com", IsActive = true }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/payments/v1/providers", registerRequest);
        var createdProvider = await createResponse.Content.ReadFromJsonAsync<ProviderResponse>();

        // Act
        var response = await _client.GetAsync($"/payments/v1/providers/{createdProvider!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var provider = await response.Content.ReadFromJsonAsync<ProviderResponse>();
        Assert.NotNull(provider);
        Assert.Equal(createdProvider.Id, provider.Id);
        Assert.Equal("Square", provider.Name);
    }

    [Fact]
    public async Task GetProviderById_WithNonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/payments/v1/providers/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProvider_WithValidData_ReturnsUpdatedProvider()
    {
        // Arrange - Register a provider
        var registerRequest = new RegisterProviderRequest
        {
            Name = "Adyen",
            DisplayName = "Adyen",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "EUR" },
            Priority = 4,
            Credentials = new Dictionary<string, string> { { "ApiKey", "test" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://api.adyen.com", IsActive = true }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/payments/v1/providers", registerRequest);
        var createdProvider = await createResponse.Content.ReadFromJsonAsync<ProviderResponse>();

        var updateRequest = new UpdateProviderRequest
        {
            DisplayName = "Adyen Payments",
            Status = ProviderStatus.Degraded,
            SupportedCurrencies = new List<string> { "USD", "EUR", "GBP" },
            Priority = 5
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/payments/v1/providers/{createdProvider!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var provider = await response.Content.ReadFromJsonAsync<ProviderResponse>();
        Assert.NotNull(provider);
        Assert.Equal("Adyen Payments", provider.DisplayName);
        Assert.Equal(ProviderStatus.Degraded, provider.Status);
        Assert.Equal(3, provider.SupportedCurrencies.Count);
        Assert.Equal(5, provider.Priority);
    }

    [Fact]
    public async Task UpdateProviderStatus_WithValidStatus_ReturnsNoContent()
    {
        // Arrange - Register a provider
        var registerRequest = new RegisterProviderRequest
        {
            Name = "Braintree",
            DisplayName = "Braintree",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 6,
            Credentials = new Dictionary<string, string> { { "MerchantId", "test" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://api.braintreegateway.com", IsActive = true }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/payments/v1/providers", registerRequest);
        var createdProvider = await createResponse.Content.ReadFromJsonAsync<ProviderResponse>();

        var statusRequest = new UpdateProviderStatusRequest
        {
            Status = ProviderStatus.Maintenance
        };

        // Act
        var response = await _client.PatchAsJsonAsync($"/payments/v1/providers/{createdProvider!.Id}/status", statusRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify status was updated
        var getResponse = await _client.GetAsync($"/payments/v1/providers/{createdProvider.Id}");
        var provider = await getResponse.Content.ReadFromJsonAsync<ProviderResponse>();
        Assert.Equal(ProviderStatus.Maintenance, provider!.Status);
    }

    [Fact]
    public async Task DeleteProvider_WithExistingId_ReturnsNoContent()
    {
        // Arrange - Register a provider
        var registerRequest = new RegisterProviderRequest
        {
            Name = "Authorize.Net",
            DisplayName = "Authorize.Net",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 7,
            Credentials = new Dictionary<string, string> { { "ApiLoginId", "test" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://api.authorize.net", IsActive = true }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/payments/v1/providers", registerRequest);
        var createdProvider = await createResponse.Content.ReadFromJsonAsync<ProviderResponse>();

        // Act
        var response = await _client.DeleteAsync($"/payments/v1/providers/{createdProvider!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify provider was deleted
        var getResponse = await _client.GetAsync($"/payments/v1/providers/{createdProvider.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetActiveByCurrency_ReturnsFilteredProviders()
    {
        // Arrange - Register multiple providers
        var provider1 = new RegisterProviderRequest
        {
            Name = "ProviderGBP",
            DisplayName = "GBP Provider",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "GBP", "EUR" },
            Priority = 1,
            Credentials = new Dictionary<string, string> { { "Key", "test" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "UK", ApiBaseUrl = "https://api.test.com", IsActive = true }
            }
        };

        var provider2 = new RegisterProviderRequest
        {
            Name = "ProviderUSD",
            DisplayName = "USD Provider",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 2,
            Credentials = new Dictionary<string, string> { { "Key", "test" } },
            Configurations = new List<RegisterProviderRequest.ProviderConfigurationDto>
            {
                new() { Region = "US", ApiBaseUrl = "https://api.test2.com", IsActive = true }
            }
        };

        await _client.PostAsJsonAsync("/payments/v1/providers", provider1);
        await _client.PostAsJsonAsync("/payments/v1/providers", provider2);

        // Act
        var response = await _client.GetAsync("/payments/v1/providers/active?currency=GBP");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var providers = await response.Content.ReadFromJsonAsync<List<ProviderSummary>>();
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);
        Assert.All(providers, p => Assert.Contains("GBP", p.SupportedCurrencies));
    }
}
