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
    public async Task ValidateWebhookAsync_PayPal_MissingHeaders_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("paypal");
        provider.Credentials["WebhookId"] = "webhook-id";

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>());

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_PayPal_MissingCertificate_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("paypal");
        provider.Credentials["WebhookId"] = "webhook-id";
        var headers = CreatePayPalHeaders("payload", "webhook-id").Headers;

        var result = await _service.ValidateWebhookAsync(provider, "payload", headers);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_PayPal_ValidSignatureWithCertificate_ShouldReturnTrue()
    {
        var provider = CreateTestProvider("paypal");
        provider.Credentials["WebhookId"] = "webhook-id";
        var signedPayload = CreatePayPalHeaders("payload", "webhook-id");
        provider.Credentials["WebhookPublicKeyPem"] = signedPayload.PublicKeyPem;

        var result = await _service.ValidateWebhookAsync(provider, "payload", signedPayload.Headers);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateWebhookAsync_Omise_InvalidIp_ShouldReturnFalse()
    {
        var provider = CreateTestProvider("omise");

        var result = await _service.ValidateWebhookAsync(
            provider, "payload", new Dictionary<string, string>(), "192.168.1.1");

        Assert.False(result);
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

    private static (Dictionary<string, string> Headers, string PublicKeyPem) CreatePayPalHeaders(string payload, string webhookId)
    {
        using var rsa = RSA.Create(2048);
        const string transmissionId = "transmission-id";
        const string transmissionTime = "2024-01-01T00:00:00Z";
        var signedData = $"{transmissionId}|{transmissionTime}|{webhookId}|{ComputeCrc32(payload)}";
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signedData),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var headers = new Dictionary<string, string>
        {
            ["PAYPAL-TRANSMISSION-ID"] = transmissionId,
            ["PAYPAL-TRANSMISSION-TIME"] = transmissionTime,
            ["PAYPAL-TRANSMISSION-SIG"] = Convert.ToBase64String(signature),
            ["PAYPAL-CERT-URL"] = "https://api.paypal.com/cert",
            ["PAYPAL-AUTH-ALGO"] = "SHA256withRSA"
        };

        return (headers, rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static uint ComputeCrc32(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        uint crc = 0xFFFFFFFF;

        foreach (var b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
            }
        }

        return ~crc;
    }
}
