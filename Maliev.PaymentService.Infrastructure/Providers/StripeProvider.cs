using System.Security.Cryptography;
using System.Text;
using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Stripe payment provider adapter.
/// Implements integration with Stripe payment gateway.
/// </summary>
public class StripeProvider : IPaymentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _apiBaseUrl;

    public string ProviderName => "stripe";

    public StripeProvider(HttpClient httpClient, string apiKey, string apiBaseUrl)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _apiBaseUrl = apiBaseUrl;
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // For MVP: Simulate Stripe payment creation
            // In production, this would call Stripe's Payment Intent API
            var providerTransactionId = $"pi_stripe_{Guid.NewGuid():N}";

            // Simulate payment processing
            await Task.Delay(100, cancellationToken); // Simulate API call latency

            return new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = providerTransactionId,
                Status = "processing",
                PaymentUrl = $"https://checkout.stripe.com/pay/{providerTransactionId}",
                RawResponse = $"{{\"id\":\"{providerTransactionId}\",\"status\":\"processing\"}}"
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
                ErrorCode = "stripe_error"
            };
        }
    }

    public async Task<ProviderPaymentStatus> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For MVP: Simulate status retrieval
            // In production, this would call Stripe's Payment Intent retrieve API
            await Task.Delay(50, cancellationToken);

            return new ProviderPaymentStatus
            {
                Status = "succeeded",
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
            // For MVP: Simulate refund processing
            // In production, this would call Stripe's Refund API
            var providerRefundId = $"re_stripe_{Guid.NewGuid():N}";

            await Task.Delay(100, cancellationToken);

            return new ProviderRefundResult
            {
                Success = true,
                ProviderRefundId = providerRefundId,
                Status = "succeeded"
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
                ErrorCode = "stripe_refund_error"
            };
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            // Stripe uses HMAC-SHA256 for webhook signature validation
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = "v1=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

            return signature.Contains(computedSignature);
        }
        catch
        {
            return false;
        }
    }
}
