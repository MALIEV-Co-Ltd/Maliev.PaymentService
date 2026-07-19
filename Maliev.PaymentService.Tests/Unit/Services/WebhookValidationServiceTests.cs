using System.Security.Cryptography;
using System.Text;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class WebhookValidationServiceTests
{
    private readonly Mock<ILogger<WebhookValidationService>> _loggerMock;
    private readonly WebhookValidationService _service;

    public WebhookValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<WebhookValidationService>>();
        _service = new WebhookValidationService(_loggerMock.Object);
    }

    [Fact]
    public async Task ValidateWebhookAsync_NullProvider_ShouldReturnFalse()
    {
        var result = await _service.ValidateWebhookAsync(
            null!, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_EmptyPayload_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("stripe");

        var result = await _service.ValidateWebhookAsync(
            provider, "", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_UnknownProvider_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("unknown-provider");
        provider.Credentials["WebhookSecret"] = "secret";

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Stripe_MissingHeader_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("stripe");
        provider.Credentials["WebhookSecret"] = "secret";

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Stripe_MissingSecret_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("stripe");
        var headers = new Dictionary<string, string> { ["Stripe-Signature"] = "sig" };

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", headers);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Omise_MissingSignature_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("omise");
        provider.Credentials["WebhookSecret"] = "secret";

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Omise_MissingSecret_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("omise");
        var headers = new Dictionary<string, string> { ["Omise-Signature"] = ComputeOmiseSignature("payload", "secret") };

        var result = await _service.ValidateWebhookAsync(provider, "payload", headers);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Omise_ValidSignature_ShouldReturnTrue()
    {
        var provider = CreateTestProvider("omise");
        provider.Credentials["WebhookSecret"] = "secret";
        var headers = new Dictionary<string, string> { ["Omise-Signature"] = ComputeOmiseSignature("payload", "secret") };

        var result = await _service.ValidateWebhookAsync(provider, "payload", headers);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Omise_DecryptionFailure_ShouldFailClosed()
    {
        var provider = CreateTestProvider("omise");
        provider.Credentials["WebhookSecret"] = "stored-secret";
        var headers = new Dictionary<string, string> { ["Omise-Signature"] = ComputeOmiseSignature("payload", "stored-secret") };
        var encryptionService = new Mock<IEncryptionService>();
        encryptionService
            .Setup(service => service.Decrypt("stored-secret"))
            .Throws(new InvalidOperationException("credential could not be decrypted"));
        var service = new WebhookValidationService(_loggerMock.Object, encryptionService.Object);

        var result = await service.ValidateWebhookAsync(provider, "payload", headers);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_OpnAlias_UsesOmiseSignatureContract()
    {
        var provider = CreateTestProvider("opn");
        provider.Credentials["WebhookSecret"] = "secret";
        var headers = new Dictionary<string, string> { ["Omise-Signature"] = ComputeOmiseSignature("payload", "secret") };

        var result = await _service.ValidateWebhookAsync(provider, "payload", headers);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Scb_MissingHeader_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("scb");
        provider.Credentials["WebhookSecret"] = "secret";

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    private static PaymentProvider CreateTestProvider(string name)
    {
        return new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = name,
            Status = ProviderStatus.Active,
            Priority = 1,
            SupportedCurrencies = new List<string> { "THB", "USD" },
            Credentials = new Dictionary<string, string>(),
            Configurations = new List<ProviderConfiguration>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string ComputeOmiseSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
