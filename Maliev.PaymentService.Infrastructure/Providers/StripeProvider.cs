using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Net.Http.Headers;

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
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = new Dictionary<string, string>(request.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["customerId"] = request.CustomerId,
                ["orderId"] = request.OrderId
            };

            var form = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = request.ReturnUrl,
                ["cancel_url"] = request.CancelUrl,
                ["client_reference_id"] = request.OrderId,
                ["line_items[0][price_data][currency]"] = request.Currency.ToLowerInvariant(),
                ["line_items[0][price_data][unit_amount]"] = ToStripeMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["line_items[0][price_data][product_data][name]"] = request.Description,
                ["line_items[0][quantity]"] = "1"
            };

            foreach (var (key, value) in metadata)
            {
                form[$"metadata[{key}]"] = value;
                form[$"payment_intent_data[metadata][{key}]"] = value;
            }

            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(
                $"{_apiBaseUrl.TrimEnd('/')}/v1/checkout/sessions",
                content,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderPaymentResult
                {
                    Success = false,
                    ProviderTransactionId = string.Empty,
                    Status = "failed",
                    ErrorMessage = responseBody,
                    ErrorCode = $"stripe_{(int)response.StatusCode}",
                    RawResponse = responseBody
                };
            }

            var session = JsonSerializer.Deserialize<StripeCheckoutSessionResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (session is null || string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.Url))
            {
                return new ProviderPaymentResult
                {
                    Success = false,
                    ProviderTransactionId = session?.Id ?? string.Empty,
                    Status = "failed",
                    ErrorMessage = "Stripe checkout session response did not include an id and url.",
                    ErrorCode = "stripe_invalid_session_response",
                    RawResponse = responseBody
                };
            }

            return new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = session.Id,
                Status = MapCheckoutStatus(session.Status),
                PaymentUrl = session.Url,
                RawResponse = responseBody
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

    private static long ToStripeMinorUnits(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static string MapCheckoutStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "complete" => "succeeded",
            "expired" => "failed",
            _ => "processing"
        };
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

    private sealed class StripeCheckoutSessionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
