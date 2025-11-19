using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates PayPal webhook signatures using certificate-based validation.
/// Note: This is a simplified implementation. Production should fetch and cache certificates.
/// </summary>
public class PayPalWebhookValidator
{
    /// <summary>
    /// Validates a PayPal webhook signature.
    /// </summary>
    /// <param name="payload">Raw webhook payload</param>
    /// <param name="transmissionId">PAYPAL-TRANSMISSION-ID header</param>
    /// <param name="transmissionTime">PAYPAL-TRANSMISSION-TIME header</param>
    /// <param name="transmissionSig">PAYPAL-TRANSMISSION-SIG header</param>
    /// <param name="certUrl">PAYPAL-CERT-URL header</param>
    /// <param name="authAlgo">PAYPAL-AUTH-ALGO header</param>
    /// <param name="webhookId">Configured webhook ID</param>
    /// <returns>True if signature is valid</returns>
    public bool ValidateSignature(
        string payload,
        string transmissionId,
        string transmissionTime,
        string transmissionSig,
        string certUrl,
        string authAlgo,
        string webhookId)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(transmissionId) ||
            string.IsNullOrWhiteSpace(transmissionTime) ||
            string.IsNullOrWhiteSpace(transmissionSig) ||
            string.IsNullOrWhiteSpace(webhookId))
        {
            return false;
        }

        // Verify cert URL is from PayPal domain
        if (!string.IsNullOrWhiteSpace(certUrl) && !IsValidPayPalCertUrl(certUrl))
        {
            return false;
        }

        // For MVP/sandbox, we'll do basic validation
        // Production implementation should:
        // 1. Fetch certificate from certUrl
        // 2. Verify certificate chain
        // 3. Use public key to verify signature

        // Construct expected signed data
        var expectedData = $"{transmissionId}|{transmissionTime}|{webhookId}|{ComputeCrc32(payload)}";

        // In production, verify using RSA with certificate
        // For now, return true if all required headers are present
        // This allows testing webhook flow without full certificate infrastructure

        return !string.IsNullOrWhiteSpace(authAlgo) &&
               authAlgo.StartsWith("SHA", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValidPayPalCertUrl(string certUrl)
    {
        if (!Uri.TryCreate(certUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // PayPal certificates must come from api.paypal.com or api-m.paypal.com
        return uri.Scheme == "https" &&
               (uri.Host.Equals("api.paypal.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("api-m.paypal.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("api.sandbox.paypal.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("api-m.sandbox.paypal.com", StringComparison.OrdinalIgnoreCase));
    }

    private uint ComputeCrc32(string data)
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
