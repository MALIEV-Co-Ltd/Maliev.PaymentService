using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates Stripe webhook signatures using HMAC SHA-256.
/// Implements Stripe's signature verification algorithm.
/// </summary>
public class StripeWebhookValidator
{
    private const int ToleranceSeconds = 300; // 5 minutes tolerance for timestamp

    /// <summary>
    /// Validates a Stripe webhook signature.
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="signature">Stripe-Signature header value</param>
    /// <param name="secret">Webhook signing secret</param>
    /// <returns>True if signature is valid and timestamp is within tolerance</returns>
    public bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        // Parse signature header: "t=timestamp,v1=signature1,v1=signature2"
        var signatureParts = ParseSignatureHeader(signature);
        if (!signatureParts.TryGetValue("t", out var timestampStr) || !signatureParts.ContainsKey("v1"))
        {
            return false;
        }

        // Verify timestamp is within tolerance
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            return false;
        }

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(currentTimestamp - timestamp) > ToleranceSeconds)
        {
            return false;
        }

        // Compute expected signature
        var signedPayload = $"{timestamp}.{payload}";
        var expectedSignature = ComputeHmacSha256(signedPayload, secret);

        // Compare with all provided signatures (Stripe may send multiple)
        var providedSignatures = signatureParts.Where(kvp => kvp.Key == "v1").Select(kvp => kvp.Value);
        return providedSignatures.Any(sig => SecureEquals(sig, expectedSignature));
    }

    private Dictionary<string, string> ParseSignatureHeader(string header)
    {
        var parts = new Dictionary<string, string>();

        foreach (var pair in header.Split(','))
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length == 2)
            {
                parts[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }

        return parts;
    }

    private string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private bool SecureEquals(string a, string b)
    {
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
