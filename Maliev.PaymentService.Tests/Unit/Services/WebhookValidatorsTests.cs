using System.Security.Cryptography;
using System.Text;
using Maliev.PaymentService.Infrastructure.Providers;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class StripeWebhookValidatorTests
{
    private readonly StripeWebhookValidator _validator;

    public StripeWebhookValidatorTests()
    {
        _validator = new StripeWebhookValidator();
    }

    [Theory]
    [InlineData("", "t=1234567890,v1=abc", "secret")]
    [InlineData("payload", "", "secret")]
    [InlineData("payload", "t=1234567890,v1=abc", "")]
    [InlineData(null, "t=1234567890,v1=abc", "secret")]
    [InlineData("payload", null, "secret")]
    [InlineData("payload", "t=1234567890,v1=abc", null)]
    public void ValidateSignature_NullOrEmpty_ShouldReturnFalse(
        string? payload, string? signature, string? secret)
    {
        var result = _validator.ValidateSignature(payload!, signature!, secret!);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_InvalidHeaderFormat_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "invalid-header", "secret");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_MissingTimestamp_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "v1=abc", "secret");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_MissingSignature_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "t=1234567890", "secret");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_TimestampOutOfRange_ShouldReturnFalse()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var result = _validator.ValidateSignature(
            "payload",
            $"t={oldTimestamp},v1=abc",
            "secret");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ShouldReturnTrue()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = "{\"event\":\"test\"}";
        var secret = "whsec_test";

        var signedPayload = $"{timestamp}.{payload}";
        var expectedSignature = ComputeHmacSha256(signedPayload, secret);

        var result = _validator.ValidateSignature(
            payload,
            $"t={timestamp},v1={expectedSignature}",
            secret);

        Assert.True(result);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}

public class PayPalWebhookValidatorTests
{
    private readonly PayPalWebhookValidator _validator;

    public PayPalWebhookValidatorTests()
    {
        _validator = new PayPalWebhookValidator();
    }

    [Theory]
    [InlineData("", "id", "time", "sig", "SHA256withRSA", "webhook", "key")]
    [InlineData("payload", "", "time", "sig", "SHA256withRSA", "webhook", "key")]
    [InlineData("payload", "id", "", "sig", "SHA256withRSA", "webhook", "key")]
    [InlineData("payload", "id", "time", "", "SHA256withRSA", "webhook", "key")]
    [InlineData("payload", "id", "time", "sig", "", "webhook", "key")]
    [InlineData("payload", "id", "time", "sig", "SHA256withRSA", "", "key")]
    [InlineData("payload", "id", "time", "sig", "SHA256withRSA", "webhook", "")]
    public void ValidateSignature_NullOrEmpty_ShouldReturnFalse(
        string payload, string transmissionId, string transmissionTime,
        string transmissionSig, string authAlgo, string webhookId, string certificatePem)
    {
        var result = _validator.ValidateSignature(
            payload, transmissionId, transmissionTime, transmissionSig,
            "https://api.paypal.com/cert", authAlgo, webhookId, certificatePem);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_InvalidCertUrl_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature(
            "payload",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            "signature",
            "http://evil.com/cert",
            "SHA256withRSA",
            "webhook-id",
            "not-a-key");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_MissingCertUrl_ShouldReturnFalse()
    {
        var (publicKeyPem, signature) = CreateSignedPayload();

        var result = _validator.ValidateSignature(
            "payload",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            signature,
            "",
            "SHA256withRSA",
            "webhook-id",
            publicKeyPem);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_ValidCertUrl_MissingAuthAlgo_ShouldReturnFalse()
    {
        var (publicKeyPem, signature) = CreateSignedPayload();

        var result = _validator.ValidateSignature(
            "payload",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            signature,
            "https://api.paypal.com/cert",
            "",
            "webhook-id",
            publicKeyPem);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_ValidPayPalSignature_WithPublicKey_ShouldReturnTrue()
    {
        var (publicKeyPem, signature) = CreateSignedPayload();

        var result = _validator.ValidateSignature(
            "payload",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            signature,
            "https://api.paypal.com/cert",
            "SHA256withRSA",
            "webhook-id",
            publicKeyPem);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_TamperedPayload_ShouldReturnFalse()
    {
        var (publicKeyPem, signature) = CreateSignedPayload();

        var result = _validator.ValidateSignature(
            "tampered",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            signature,
            "https://api.paypal.com/cert",
            "SHA256withRSA",
            "webhook-id",
            publicKeyPem);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_HeaderOnlyWithoutCryptographicProof_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature(
            "payload",
            "transmission-id",
            "2024-01-01T00:00:00Z",
            "not-base64",
            "https://api.paypal.com/cert",
            "SHA256withRSA",
            "webhook-id",
            "not-a-key");

        Assert.False(result);
    }

    [Theory]
    [InlineData("https://api.paypal.com/webhooks/id")]
    [InlineData("https://api-m.paypal.com/webhooks/id")]
    [InlineData("https://api.sandbox.paypal.com/webhooks/id")]
    [InlineData("https://api-m.sandbox.paypal.com/webhooks/id")]
    public void ValidateSignature_ValidPayPalDomains_WithValidSignature_ShouldReturnTrue(string certUrl)
    {
        var (publicKeyPem, signature) = CreateSignedPayload("payload", "id", "time", "webhook");

        var result = _validator.ValidateSignature(
            "payload", "id", "time", signature,
            certUrl, "SHA256withRSA", "webhook", publicKeyPem);

        Assert.True(result);
    }

    private static (string PublicKeyPem, string Signature) CreateSignedPayload(
        string payload = "payload",
        string transmissionId = "transmission-id",
        string transmissionTime = "2024-01-01T00:00:00Z",
        string webhookId = "webhook-id")
    {
        using var rsa = RSA.Create(2048);
        var signedData = $"{transmissionId}|{transmissionTime}|{webhookId}|{ComputeCrc32(payload)}";
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signedData),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return (rsa.ExportSubjectPublicKeyInfoPem(), Convert.ToBase64String(signature));
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

public class ScbWebhookValidatorTests
{
    private readonly ScbWebhookValidator _validator;

    public ScbWebhookValidatorTests()
    {
        _validator = new ScbWebhookValidator();
    }

    [Theory]
    [InlineData("", "sig", "secret")]
    [InlineData("payload", "", "secret")]
    [InlineData("payload", "sig", "")]
    [InlineData(null, "sig", "secret")]
    [InlineData("payload", null, "secret")]
    [InlineData("payload", "sig", null)]
    public void ValidateSignature_NullOrEmpty_ShouldReturnFalse(
        string? payload, string? signature, string? secret)
    {
        var result = _validator.ValidateSignature(payload!, signature!, secret!);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_InvalidTimestamp_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature(
            "payload", "signature", "secret", "not-a-number");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_TimestampOutOfRange_ShouldReturnFalse()
    {
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var result = _validator.ValidateSignature(
            "payload", "signature", "secret", oldTimestamp.ToString());

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ShouldReturnTrue()
    {
        var payload = "{\"event\":\"test\"}";
        var secret = "test-secret";

        var expectedSignature = ComputeHmacSha256(payload, secret);

        var result = _validator.ValidateSignature(
            payload, expectedSignature, secret);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_WithTimestampAndRequestId_ShouldUseInSigning()
    {
        var payload = "{\"event\":\"test\"}";
        var secret = "test-secret";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var requestId = Guid.NewGuid().ToString();

        var dataToSign = $"{timestamp}.{requestId}.{payload}";
        var expectedSignature = ComputeHmacSha256(dataToSign, secret);

        var result = _validator.ValidateSignature(
            payload, expectedSignature, secret, timestamp, requestId);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_EmptySignature_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature(
            "payload", "", "secret");

        Assert.False(result);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}

public class OmiseWebhookValidatorTests
{
    private readonly OmiseWebhookValidator _validator;

    public OmiseWebhookValidatorTests()
    {
        _validator = new OmiseWebhookValidator();
    }

    [Fact]
    public void ValidateIpAddress_NullOrEmpty_ShouldReturnFalse()
    {
        Assert.False(_validator.ValidateIpAddress(null));
        Assert.False(_validator.ValidateIpAddress(""));
        Assert.False(_validator.ValidateIpAddress("   "));
    }

    [Theory]
    [InlineData("52.74.115.100")]
    [InlineData("54.151.127.36")]
    [InlineData("54.169.162.201")]
    [InlineData("13.228.81.94")]
    [InlineData("18.141.73.155")]
    [InlineData("13.229.37.222")]
    public void ValidateIpAddress_WhitelistedIp_ShouldReturnTrue(string ip)
    {
        var result = _validator.ValidateIpAddress(ip);

        Assert.True(result);
    }

    [Fact]
    public void ValidateIpAddress_InvalidIp_ShouldReturnFalse()
    {
        var result = _validator.ValidateIpAddress("not.an.ip.address");

        Assert.False(result);
    }

    [Theory]
    [InlineData("52.74.1.1")]
    [InlineData("52.74.255.255")]
    [InlineData("54.151.1.1")]
    [InlineData("13.228.1.1")]
    public void ValidateIpAddress_InCidrRange_ShouldReturnTrue(string ip)
    {
        var result = _validator.ValidateIpAddress(ip);

        Assert.True(result);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("8.8.8.8")]
    public void ValidateIpAddress_NotInRange_ShouldReturnFalse(string ip)
    {
        var result = _validator.ValidateIpAddress(ip);

        Assert.False(result);
    }

    [Fact]
    public void ValidateIpAddress_WithPort_ShouldStripPort()
    {
        var result = _validator.ValidateIpAddress("52.74.115.100:8080");

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_NoSignature_ShouldReturnTrue()
    {
        var result = _validator.ValidateSignature("payload", null, "secret");

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_NoSecret_ShouldReturnTrue()
    {
        var result = _validator.ValidateSignature("payload", "signature", null);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_BothEmpty_ShouldReturnTrue()
    {
        var result = _validator.ValidateSignature("payload", "", "");

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ShouldReturnTrue()
    {
        var payload = "{\"event\":\"test\"}";
        var secret = "secret";

        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToBase64String(computedHash);

        var result = _validator.ValidateSignature(payload, signature, secret);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_InvalidSignature_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "invalid-signature", "secret");

        Assert.False(result);
    }
}
