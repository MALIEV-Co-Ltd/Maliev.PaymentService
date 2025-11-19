using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Encryption;
using Maliev.PaymentService.Infrastructure.Services;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

/// <summary>
/// Unit tests for ProviderManagementService.
/// Tests provider registration, update, credential encryption.
/// </summary>
public class ProviderManagementServiceTests
{
    private readonly Mock<IProviderRepository> _mockRepository;
    private readonly Mock<IEncryptionService> _mockEncryption;
    private readonly ProviderManagementService _service;

    public ProviderManagementServiceTests()
    {
        _mockRepository = new Mock<IProviderRepository>();
        _mockEncryption = new Mock<IEncryptionService>();
        _service = new ProviderManagementService(_mockRepository.Object, _mockEncryption.Object);
    }

    [Fact]
    public async Task RegisterProviderAsync_EncryptsCredentials()
    {
        // Arrange
        var provider = new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = "Stripe",
            DisplayName = "Stripe Payments",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "EUR" },
            Priority = 1,
            Credentials = new Dictionary<string, string>
            {
                { "ApiKey", "sk_test_123456" },
                { "WebhookSecret", "whsec_123456" }
            },
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockEncryption
            .Setup(e => e.Encrypt(It.IsAny<string>()))
            .Returns<string>(s => $"encrypted_{s}");

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<PaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        // Act
        var result = await _service.RegisterProviderAsync(provider);

        // Assert
        Assert.NotNull(result);
        _mockEncryption.Verify(e => e.Encrypt("sk_test_123456"), Times.Once);
        _mockEncryption.Verify(e => e.Encrypt("whsec_123456"), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(It.Is<PaymentProvider>(p =>
            p.Credentials["ApiKey"].StartsWith("encrypted_") &&
            p.Credentials["WebhookSecret"].StartsWith("encrypted_")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderByIdAsync_DecryptsCredentials()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var encryptedProvider = new PaymentProvider
        {
            Id = providerId,
            Name = "PayPal",
            DisplayName = "PayPal",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 2,
            Credentials = new Dictionary<string, string>
            {
                { "ClientId", "encrypted_client_id" },
                { "ClientSecret", "encrypted_client_secret" }
            },
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(encryptedProvider);

        _mockEncryption
            .Setup(e => e.Decrypt("encrypted_client_id"))
            .Returns("client_id_123");

        _mockEncryption
            .Setup(e => e.Decrypt("encrypted_client_secret"))
            .Returns("client_secret_456");

        // Act
        var result = await _service.GetProviderByIdAsync(providerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("client_id_123", result.Credentials["ClientId"]);
        Assert.Equal("client_secret_456", result.Credentials["ClientSecret"]);
        _mockEncryption.Verify(e => e.Decrypt("encrypted_client_id"), Times.Once);
        _mockEncryption.Verify(e => e.Decrypt("encrypted_client_secret"), Times.Once);
    }

    [Fact]
    public async Task UpdateProviderAsync_ReencryptsCredentialsIfChanged()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var existingProvider = new PaymentProvider
        {
            Id = providerId,
            Name = "Square",
            DisplayName = "Square",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 3,
            Credentials = new Dictionary<string, string>
            {
                { "AccessToken", "encrypted_old_token" }
            },
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedProvider = new PaymentProvider
        {
            Id = providerId,
            Name = "Square",
            DisplayName = "Square Payments",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD", "EUR" },
            Priority = 4,
            Credentials = new Dictionary<string, string>
            {
                { "AccessToken", "new_token_123" }
            },
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = existingProvider.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProvider);

        _mockEncryption
            .Setup(e => e.Encrypt("new_token_123"))
            .Returns("encrypted_new_token");

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedProvider);

        // Act
        var result = await _service.UpdateProviderAsync(updatedProvider);

        // Assert
        Assert.NotNull(result);
        _mockEncryption.Verify(e => e.Encrypt("new_token_123"), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<PaymentProvider>(p =>
            p.Credentials["AccessToken"] == "encrypted_new_token"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllProvidersAsync_ReturnsAllProviders()
    {
        // Arrange
        var providers = new List<PaymentProvider>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Provider1",
                DisplayName = "Provider 1",
                Status = ProviderStatus.Active,
                SupportedCurrencies = new List<string> { "USD" },
                Priority = 1,
                Credentials = new Dictionary<string, string>(),
                Configurations = new List<ProviderConfiguration>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Provider2",
                DisplayName = "Provider 2",
                Status = ProviderStatus.Active,
                SupportedCurrencies = new List<string> { "EUR" },
                Priority = 2,
                Credentials = new Dictionary<string, string>(),
                Configurations = new List<ProviderConfiguration>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);

        // Act
        var result = await _service.GetAllProvidersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetActiveByCurrencyAsync_ReturnsFilteredProviders()
    {
        // Arrange
        var currency = "GBP";
        var providers = new List<PaymentProvider>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "UKProvider",
                DisplayName = "UK Provider",
                Status = ProviderStatus.Active,
                SupportedCurrencies = new List<string> { "GBP", "EUR" },
                Priority = 1,
                Credentials = new Dictionary<string, string>(),
                Configurations = new List<ProviderConfiguration>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockRepository
            .Setup(r => r.GetActiveByCurrencyAsync(currency, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);

        // Act
        var result = await _service.GetActiveByCurrencyAsync(currency);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.All(result, p => Assert.Contains(currency, p.SupportedCurrencies));
        _mockRepository.Verify(r => r.GetActiveByCurrencyAsync(currency, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProviderStatusAsync_UpdatesStatus()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new PaymentProvider
        {
            Id = providerId,
            Name = "TestProvider",
            DisplayName = "Test Provider",
            Status = ProviderStatus.Active,
            SupportedCurrencies = new List<string> { "USD" },
            Priority = 1,
            Credentials = new Dictionary<string, string>(),
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<PaymentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        // Act
        await _service.UpdateProviderStatusAsync(providerId, ProviderStatus.Maintenance);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<PaymentProvider>(p =>
            p.Id == providerId && p.Status == ProviderStatus.Maintenance
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProviderAsync_DeletesProvider()
    {
        // Arrange
        var providerId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteAsync(providerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteProviderAsync(providerId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(providerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var providerId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentProvider?)null);

        // Act
        var result = await _service.GetProviderByIdAsync(providerId);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByIdAsync(providerId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
