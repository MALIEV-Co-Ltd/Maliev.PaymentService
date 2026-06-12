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
                ["billing_address_collection"] = "required",
                ["customer_creation"] = "always",
                ["phone_number_collection[enabled]"] = "true",
                ["tax_id_collection[enabled]"] = "true",
                ["name_collection[individual][enabled]"] = "true",
                ["name_collection[individual][optional]"] = "false",
                ["name_collection[business][enabled]"] = "true",
                ["name_collection[business][optional]"] = "true",
                ["consent_collection[terms_of_service]"] = "required",
                ["shipping_address_collection[allowed_countries][0]"] = "TH",
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
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl.TrimEnd('/')}/v1/checkout/sessions")
            {
                Content = content
            };
            httpRequest.Headers.Add("Idempotency-Key", request.IdempotencyKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

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
            using var response = await _httpClient.GetAsync(
                $"{_apiBaseUrl.TrimEnd('/')}/v1/checkout/sessions/{Uri.EscapeDataString(providerTransactionId)}",
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderPaymentStatus
                {
                    Status = "failed",
                    ErrorMessage = responseBody
                };
            }

            var session = JsonSerializer.Deserialize<StripeCheckoutSessionStatusResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (session is null)
            {
                return new ProviderPaymentStatus
                {
                    Status = "failed",
                    ErrorMessage = "Stripe checkout session status response could not be parsed."
                };
            }

            return new ProviderPaymentStatus
            {
                Status = MapCheckoutPaymentStatus(session.PaymentStatus, session.Status),
                CompletedAt = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                    ? DateTime.UtcNow
                    : null
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

    private static string MapCheckoutPaymentStatus(string? paymentStatus, string? checkoutStatus)
    {
        if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return "succeeded";
        }

        if (string.Equals(checkoutStatus, "expired", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        return "processing";
    }

    public async Task<ProviderRefundResult> ProcessRefundAsync(ProviderRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var form = new Dictionary<string, string>
            {
                ["amount"] = ToStripeMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["reason"] = NormalizeRefundReason(request.Reason)
            };

            var paymentIntentId = request.ProviderTransactionId;
            if (request.ProviderTransactionId.StartsWith("cs_", StringComparison.OrdinalIgnoreCase))
            {
                paymentIntentId = await ResolveCheckoutSessionPaymentIntentAsync(
                    request.ProviderTransactionId,
                    cancellationToken);
            }

            if (paymentIntentId.StartsWith("ch_", StringComparison.OrdinalIgnoreCase))
            {
                form["charge"] = paymentIntentId;
            }
            else
            {
                form["payment_intent"] = paymentIntentId;
            }

            foreach (var (key, value) in request.Metadata ?? new Dictionary<string, string>())
            {
                form[$"metadata[{key}]"] = value;
            }

            using var content = new FormUrlEncodedContent(form);
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl.TrimEnd('/')}/v1/refunds")
            {
                Content = content
            };
            httpRequest.Headers.Add("Idempotency-Key", request.IdempotencyKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderRefundResult
                {
                    Success = false,
                    ProviderRefundId = string.Empty,
                    Status = "failed",
                    ErrorMessage = responseBody,
                    ErrorCode = $"stripe_refund_{(int)response.StatusCode}"
                };
            }

            var refund = JsonSerializer.Deserialize<StripeRefundResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (refund is null || string.IsNullOrWhiteSpace(refund.Id))
            {
                return new ProviderRefundResult
                {
                    Success = false,
                    ProviderRefundId = string.Empty,
                    Status = "failed",
                    ErrorMessage = "Stripe refund response did not include an id.",
                    ErrorCode = "stripe_invalid_refund_response"
                };
            }

            return new ProviderRefundResult
            {
                Success = true,
                ProviderRefundId = refund.Id,
                Status = refund.Status ?? "processing"
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

    private async Task<string> ResolveCheckoutSessionPaymentIntentAsync(
        string checkoutSessionId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"{_apiBaseUrl.TrimEnd('/')}/v1/checkout/sessions/{Uri.EscapeDataString(checkoutSessionId)}",
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Stripe checkout session lookup failed: {responseBody}");
        }

        var session = JsonSerializer.Deserialize<StripeCheckoutSessionRefundLookupResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (session is null || string.IsNullOrWhiteSpace(session.PaymentIntent))
        {
            throw new InvalidOperationException("Stripe checkout session did not include a payment_intent for refund.");
        }

        return session.PaymentIntent;
    }

    private static string NormalizeRefundReason(string reason)
    {
        return reason.Trim().ToLowerInvariant() switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" or "customer" or "customer_requested" => "requested_by_customer",
            _ => "requested_by_customer"
        };
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

    private sealed class StripeCheckoutSessionStatusResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("payment_status")]
        public string? PaymentStatus { get; set; }
    }

    private sealed class StripeCheckoutSessionRefundLookupResponse
    {
        [JsonPropertyName("payment_intent")]
        public string? PaymentIntent { get; set; }
    }

    private sealed class StripeRefundResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
