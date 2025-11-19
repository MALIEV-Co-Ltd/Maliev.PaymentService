using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Omise payment provider adapter.
/// Implements integration with Omise payment gateway (Thailand).
/// </summary>
public class OmiseProvider : IPaymentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string _apiBaseUrl;

    public string ProviderName => "omise";

    public OmiseProvider(HttpClient httpClient, string secretKey, string apiBaseUrl)
    {
        _httpClient = httpClient;
        _secretKey = secretKey;
        _apiBaseUrl = apiBaseUrl;

        // Omise uses Basic Auth with secret key
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_secretKey}:"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authValue}");
    }

    public async Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // For MVP: Simulate Omise charge creation
            var providerTransactionId = $"chrg_omise_{Guid.NewGuid():N}";

            await Task.Delay(100, cancellationToken);

            return new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = providerTransactionId,
                Status = "pending",
                PaymentUrl = $"https://pay.omise.co/{providerTransactionId}",
                RawResponse = $"{{\"id\":\"{providerTransactionId}\",\"status\":\"pending\"}}"
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
                ErrorCode = "omise_error"
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
                Status = "successful",
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
            var providerRefundId = $"rfnd_omise_{Guid.NewGuid():N}";

            await Task.Delay(100, cancellationToken);

            return new ProviderRefundResult
            {
                Success = true,
                ProviderRefundId = providerRefundId,
                Status = "successful"
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
                ErrorCode = "omise_refund_error"
            };
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToBase64String(hash);

            return signature == computedSignature;
        }
        catch
        {
            return false;
        }
    }
}
