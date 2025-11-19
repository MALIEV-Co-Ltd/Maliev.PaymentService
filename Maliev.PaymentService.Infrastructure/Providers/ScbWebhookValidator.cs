using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates SCB (Siam Commercial Bank) webhook signatures using HMAC-SHA256.
/// SCB Easy App webhooks include a signature in the request headers.
/// </summary>
public class ScbWebhookValidator
{
    /// <summary>
    /// Validates an SCB webhook signature.
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="signature">X-SCB-Signature header value</param>
    /// <param name="secret">Webhook signing secret provided by SCB</param>
    /// <param name="timestamp">X-SCB-Timestamp header value (optional for replay attack prevention)</param>
    /// <param name="requestId">X-SCB-Request-ID header value (optional for deduplication)</param>
    /// <returns>True if signature is valid</returns>
    public bool ValidateSignature(
        string payload,
        string signature,
        string secret,
        string? timestamp = null,
        string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        // If timestamp provided, verify it's within acceptable range (5 minutes)
        if (!string.IsNullOrWhiteSpace(timestamp))
        {
            if (!long.TryParse(timestamp, out var unixTimestamp))
            {
                return false;
            }

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            var timeDifference = Math.Abs((DateTimeOffset.UtcNow - requestTime).TotalSeconds);

            if (timeDifference > 300) // 5 minutes tolerance
            {
                return false;
            }
        }

        // Construct the string to sign
        // SCB format: {timestamp}.{requestId}.{payload} or just {payload} if no headers
        string dataToSign;
        if (!string.IsNullOrWhiteSpace(timestamp) && !string.IsNullOrWhiteSpace(requestId))
        {
            dataToSign = $"{timestamp}.{requestId}.{payload}";
        }
        else
        {
            dataToSign = payload;
        }

        // Compute HMAC-SHA256
        var expectedSignature = ComputeHmacSha256(dataToSign, secret);

        // Compare signatures using timing-safe comparison
        return SecureEquals(signature, expectedSignature);
    }

    private string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        // SCB expects hex-encoded signature
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private bool SecureEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }

        // Handle different casing
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
