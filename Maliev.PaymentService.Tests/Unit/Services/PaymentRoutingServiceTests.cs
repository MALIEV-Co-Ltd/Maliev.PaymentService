using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Services;
using Maliev.PaymentService.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class PaymentRoutingServiceTests
{
    private readonly Mock<IProviderRepository> _providerRepositoryMock;
    private readonly CircuitBreakerStateManager _circuitBreakerStateManager;
    private readonly Mock<ILogger<PaymentRoutingService>> _loggerMock;
    private readonly PaymentRoutingService _service;

    public PaymentRoutingServiceTests()
    {
        _providerRepositoryMock = new Mock<IProviderRepository>();
        _circuitBreakerStateManager = new CircuitBreakerStateManager();
        _loggerMock = new Mock<ILogger<PaymentRoutingService>>();

        _service = new PaymentRoutingService(
            _providerRepositoryMock.Object,
            _circuitBreakerStateManager,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SelectProviderAsync_WithNoProviders_ShouldThrowException()
    {
        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SelectProviderAsync("THB"));
    }

    [Fact]
    public async Task SelectProviderAsync_WithPreferredProvider_CanSelectPreferred()
    {
        var provider = CreateTestProvider("omise", ProviderStatus.Active, 1);

        _providerRepositoryMock
            .Setup(r => r.GetActiveByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { provider });

        var result = await _service.SelectProviderAsync("THB", "omise");

        Assert.Equal("omise", result.Name);
    }

    [Fact]
    public async Task SelectProviderAsync_ThailandCurrency_IgnoresStripePreferenceWhenOmiseIsHealthy()
    {
        var stripe = CreateTestProvider("stripe", ProviderStatus.Active, 1);
        var omise = CreateTestProvider("omise", ProviderStatus.Active, 2);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { stripe, omise });

        var result = await _service.SelectProviderAsync("THB", "stripe");

        Assert.Equal("omise", result.Name);
    }

    [Fact]
    public async Task SelectProviderAsync_ThailandCurrency_FallsBackToStripeWhenOmiseCircuitIsOpen()
    {
        var omise = CreateTestProvider("omise", ProviderStatus.Active, 1);
        var stripe = CreateTestProvider("stripe", ProviderStatus.Active, 2);
        _circuitBreakerStateManager.RecordStateChange("omise", true, DateTime.UtcNow);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { omise, stripe });

        var result = await _service.SelectProviderAsync("THB", "stripe");

        Assert.Equal("stripe", result.Name);
    }

    [Fact]
    public async Task SelectProviderAsync_WithPreferredProviderCircuitOpen_ShouldFallback()
    {
        var provider = CreateTestProvider("stripe", ProviderStatus.Active, 1);
        _circuitBreakerStateManager.RecordStateChange("stripe", true, DateTime.UtcNow);

        var provider2 = CreateTestProvider("omise", ProviderStatus.Active, 2);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { provider, provider2 });

        var result = await _service.SelectProviderAsync("THB", "stripe");

        Assert.Equal("omise", result.Name);
    }

    [Fact]
    public async Task SelectProviderAsync_AllCircuitsOpen_ShouldUseDegradedProvider()
    {
        var provider = CreateTestProvider("stripe", ProviderStatus.Active, 1);
        _circuitBreakerStateManager.RecordStateChange("stripe", true, DateTime.UtcNow);

        var degradedProvider = CreateTestProvider("omise", ProviderStatus.Degraded, 2);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { provider, degradedProvider });

        var result = await _service.SelectProviderAsync("THB");

        Assert.Equal("omise", result.Name);
        Assert.Equal(ProviderStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task SelectProviderAsync_AllCircuitsOpen_NoDegraded_ShouldThrowException()
    {
        var provider = CreateTestProvider("stripe", ProviderStatus.Active, 1);
        _circuitBreakerStateManager.RecordStateChange("stripe", true, DateTime.UtcNow);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { provider });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SelectProviderAsync("THB"));
    }

    [Fact]
    public async Task SelectProviderAsync_MultipleProviders_SelectsFirstByDefault()
    {
        var stripe = CreateTestProvider("stripe", ProviderStatus.Active, 2);
        var omise = CreateTestProvider("omise", ProviderStatus.Active, 1);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { omise, stripe });

        var result = await _service.SelectProviderAsync("THB");

        Assert.Equal("omise", result.Name);
    }

    [Fact]
    public async Task SelectProviderAsync_DisabledProvider_NotSelected()
    {
        var activeProvider = CreateTestProvider("omise", ProviderStatus.Active, 1);
        var disabledProvider = CreateTestProvider("stripe", ProviderStatus.Disabled, 1);

        _providerRepositoryMock
            .Setup(r => r.GetRoutableByCurrencyAsync("THB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentProvider> { activeProvider, disabledProvider });

        var result = await _service.SelectProviderAsync("THB");

        Assert.Equal("omise", result.Name);
    }

    private static PaymentProvider CreateTestProvider(string name, ProviderStatus status, int priority)
    {
        return new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = name,
            Status = status,
            Priority = priority,
            SupportedCurrencies = new List<string> { "THB", "USD" },
            Credentials = new Dictionary<string, string>(),
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
