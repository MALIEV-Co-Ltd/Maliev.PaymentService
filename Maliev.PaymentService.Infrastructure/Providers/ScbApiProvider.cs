using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// SCB (Siam Commercial Bank) API payment provider adapter.
/// Implements integration with SCB payment gateway (Thailand).
/// </summary>
public class ScbApiProvider : IPaymentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _apiBaseUrl;

    public string ProviderName => "scb";

    public ScbApiProvider(HttpClient httpClient, string apiKey, string apiSecret, string apiBaseUrl)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _apiBaseUrl = apiBaseUrl;
    }

    public async Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // For MVP: Simulate SCB payment creation
            // In production, this would call SCB's Payment API with OAuth 2.0
            var providerTransactionId = $"SCB{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}".Substring(0, 32);

            await Task.Delay(100, cancellationToken);

            return new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = providerTransactionId,
                Status = "pending",
                PaymentUrl = $"https://pay.scb.co.th/qr/{providerTransactionId}",
                RawResponse = $"{{\"transactionId\":\"{providerTransactionId}\",\"status\":\"PENDING\"}}"
            };
        }
        catch (Exception ex)
        {
            return new ProviderPaymentResult
            {
                Success = false,
                ProviderTransactionId = string.Empty,
                Status = "failed",
                ErrorMessage = ex.Message,
                ErrorCode = "scb_error"
            };
        }
    }

    public async Task<ProviderPaymentStatus> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(50, cancellationToken);

            return new ProviderPaymentStatus
            {
                Status = "success",
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new ProviderPaymentStatus
            {
                Status = "failed",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ProviderRefundResult> ProcessRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var providerRefundId = $"SCBRF{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}".Substring(0, 32);

            await Task.Delay(100, cancellationToken);

            return new ProviderRefundResult
            {
                Success = true,
                ProviderRefundId = providerRefundId,
                Status = "success"
            };
        }
        catch (Exception ex)
        {
            return new ProviderRefundResult
            {
                Success = false,
                ProviderRefundId = string.Empty,
                Status = "failed",
                ErrorMessage = ex.Message,
                ErrorCode = "scb_refund_error"
            };
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            // SCB webhook validation
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return signature.Equals(computedSignature, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
