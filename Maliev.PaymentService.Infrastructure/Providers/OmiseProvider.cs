using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    public async Task<ProviderPaymentResult> ProcessPaymentAsync(ProviderPaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = new Dictionary<string, string>(
                request.Metadata ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
            {
                ["customerId"] = request.CustomerId,
                ["orderId"] = request.OrderId
            };

            var sourceType = ResolveSourceType(request);
            var form = new Dictionary<string, string>
            {
                ["amount"] = ToOmiseMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["currency"] = request.Currency.ToLowerInvariant(),
                ["source[type]"] = sourceType,
                ["return_uri"] = request.ReturnUrl,
                ["description"] = request.Description
            };

            foreach (var (key, value) in metadata)
            {
                form[$"metadata[{key}]"] = value;
            }

            using var content = new FormUrlEncodedContent(form);
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl.TrimEnd('/')}/charges")
            {
                Content = content
            };
            AddIdempotencyKey(httpRequest, request.IdempotencyKey);

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
                    ErrorCode = $"omise_{(int)response.StatusCode}",
                    RawResponse = responseBody
                };
            }

            var charge = JsonSerializer.Deserialize<OmiseChargeResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (charge is null || string.IsNullOrWhiteSpace(charge.Id))
            {
                return new ProviderPaymentResult
                {
                    Success = false,
                    ProviderTransactionId = charge?.Id ?? string.Empty,
                    Status = "failed",
                    ErrorMessage = "Omise charge response did not include an id.",
                    ErrorCode = "omise_invalid_charge_response",
                    RawResponse = responseBody
                };
            }

            return new ProviderPaymentResult
            {
                Success = true,
                ProviderTransactionId = charge.Id,
                Status = charge.Status ?? "pending",
                PaymentUrl = ResolvePaymentUrl(charge),
                QrImageUrl = charge.Source?.ScannableCode?.Image?.DownloadUri,
                QrRawData = charge.Source?.ScannableCode?.RawData,
                ExpiresAt = charge.ExpiresAt,
                PaymentMethod = sourceType,
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
                ErrorCode = "omise_error"
            };
        }
    }

    public async Task<ProviderPaymentStatus> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{_apiBaseUrl.TrimEnd('/')}/charges/{Uri.EscapeDataString(providerTransactionId)}",
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

            var charge = JsonSerializer.Deserialize<OmiseChargeResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (charge is null || string.IsNullOrWhiteSpace(charge.Status))
            {
                return new ProviderPaymentStatus
                {
                    Status = "failed",
                    ErrorMessage = "Omise charge status response could not be parsed."
                };
            }

            return new ProviderPaymentStatus
            {
                Status = charge.Status,
                CompletedAt = IsCompletedStatus(charge.Status) ? DateTime.UtcNow : null
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
            var metadata = new Dictionary<string, string>(
                request.Metadata ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
            {
                ["reason"] = request.Reason,
                ["idempotencyKey"] = request.IdempotencyKey
            };

            var form = new Dictionary<string, string>
            {
                ["amount"] = ToOmiseMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture)
            };

            foreach (var (key, value) in metadata)
            {
                form[$"metadata[{key}]"] = value;
            }

            using var content = new FormUrlEncodedContent(form);
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBaseUrl.TrimEnd('/')}/charges/{Uri.EscapeDataString(request.ProviderTransactionId)}/refunds")
            {
                Content = content
            };
            AddIdempotencyKey(httpRequest, request.IdempotencyKey);

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
                    ErrorCode = $"omise_refund_{(int)response.StatusCode}"
                };
            }

            var refund = JsonSerializer.Deserialize<OmiseRefundResponse>(
                responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (refund is null || string.IsNullOrWhiteSpace(refund.Id))
            {
                return new ProviderRefundResult
                {
                    Success = false,
                    ProviderRefundId = string.Empty,
                    Status = "failed",
                    ErrorMessage = "Omise refund response did not include an id.",
                    ErrorCode = "omise_invalid_refund_response"
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
                ErrorCode = "omise_refund_error"
            };
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload) ||
                string.IsNullOrWhiteSpace(signature) ||
                string.IsNullOrWhiteSpace(secret))
            {
                return false;
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();
            var computedBytes = Encoding.UTF8.GetBytes(computedSignature);

            foreach (var candidate in signature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var candidateBytes = Encoding.UTF8.GetBytes(candidate.ToLowerInvariant());
                if (candidateBytes.Length == computedBytes.Length &&
                    CryptographicOperations.FixedTimeEquals(candidateBytes, computedBytes))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static long ToOmiseMinorUnits(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static void AddIdempotencyKey(HttpRequestMessage request, string idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
    }

    private static string ResolveSourceType(ProviderPaymentRequest request)
    {
        if (request.Metadata != null &&
            (request.Metadata.TryGetValue("omiseSourceType", out var sourceType) ||
             request.Metadata.TryGetValue("opnSourceType", out sourceType)) &&
            !string.IsNullOrWhiteSpace(sourceType))
        {
            return sourceType;
        }

        return "promptpay";
    }

    private static string? ResolvePaymentUrl(OmiseChargeResponse charge)
    {
        if (!string.IsNullOrWhiteSpace(charge.AuthorizeUri))
        {
            return charge.AuthorizeUri;
        }

        return charge.Source?.ScannableCode?.Image?.DownloadUri;
    }

    private static bool IsCompletedStatus(string status)
    {
        return string.Equals(status, "successful", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OmiseChargeResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("authorize_uri")]
        public string? AuthorizeUri { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("source")]
        public OmiseSourceResponse? Source { get; set; }
    }

    private sealed class OmiseSourceResponse
    {
        [JsonPropertyName("scannable_code")]
        public OmiseScannableCodeResponse? ScannableCode { get; set; }
    }

    private sealed class OmiseScannableCodeResponse
    {
        [JsonPropertyName("image")]
        public OmiseImageResponse? Image { get; set; }

        [JsonPropertyName("raw_data")]
        public string? RawData { get; set; }
    }

    private sealed class OmiseImageResponse
    {
        [JsonPropertyName("download_uri")]
        public string? DownloadUri { get; set; }
    }

    private sealed class OmiseRefundResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
