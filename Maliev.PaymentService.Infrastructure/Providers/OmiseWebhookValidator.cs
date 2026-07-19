using System.Security.Cryptography;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates Omise webhook requests using the Omise-Signature HMAC header.
/// </summary>
public class OmiseWebhookValidator
{
    /// <summary>
    /// Validates the Omise webhook signature.
    /// </summary>
    /// <param name="payload">Raw webhook payload.</param>
    /// <param name="signatureHeader">Omise-Signature header value.</param>
    /// <param name="secret">Configured Omise webhook secret.</param>
    /// <returns>True when the payload matches one of the supplied HMAC signatures.</returns>
    public bool ValidateSignature(string? payload, string? signatureHeader, string? secret)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(signatureHeader) ||
            string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();
        var computedBytes = Encoding.UTF8.GetBytes(computedSignature);

        foreach (var signature in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var providedBytes = Encoding.UTF8.GetBytes(signature.ToLowerInvariant());
            if (providedBytes.Length == computedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes))
            {
                return true;
            }
        }

        return false;
    }
}
