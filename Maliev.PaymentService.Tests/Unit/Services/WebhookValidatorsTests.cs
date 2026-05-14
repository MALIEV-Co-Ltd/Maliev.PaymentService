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
    public void ValidateSignature_NoSignature_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", null, "secret");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_NoSecret_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "signature", null);

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_BothEmpty_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "", "");

        Assert.False(result);
    }

    [Fact]
    public void ValidateSignature_ValidSignature_ShouldReturnTrue()
    {
        var payload = "{\"event\":\"test\"}";
        var secret = "secret";

        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexString(computedHash).ToLowerInvariant();

        var result = _validator.ValidateSignature(payload, signature, secret);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_ValidRotatedSignatureList_ShouldReturnTrue()
    {
        var payload = "{\"event\":\"test\"}";
        var secret = "secret";

        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToHexString(computedHash).ToLowerInvariant();

        var result = _validator.ValidateSignature(payload, $"old-signature, {signature}", secret);

        Assert.True(result);
    }

    [Fact]
    public void ValidateSignature_InvalidSignature_ShouldReturnFalse()
    {
        var result = _validator.ValidateSignature("payload", "invalid-signature", "secret");

        Assert.False(result);
    }
}
