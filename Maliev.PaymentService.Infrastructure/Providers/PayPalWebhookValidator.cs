using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates PayPal webhook signatures using certificate-based validation.
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
    /// <param name="certificatePem">Configured PayPal webhook certificate/public key PEM.</param>
    /// <returns>True if signature is valid</returns>
    public bool ValidateSignature(
        string payload,
        string transmissionId,
        string transmissionTime,
        string transmissionSig,
        string certUrl,
        string authAlgo,
        string webhookId,
        string certificatePem)
    {
        if (string.IsNullOrWhiteSpace(payload) ||
            string.IsNullOrWhiteSpace(transmissionId) ||
            string.IsNullOrWhiteSpace(transmissionTime) ||
            string.IsNullOrWhiteSpace(transmissionSig) ||
            string.IsNullOrWhiteSpace(webhookId) ||
            string.IsNullOrWhiteSpace(certificatePem))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(certUrl) || string.IsNullOrWhiteSpace(authAlgo))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(certUrl) && !IsValidPayPalCertUrl(certUrl))
        {
            return false;
        }

        if (!TryGetHashAlgorithm(authAlgo, out var hashAlgorithm))
        {
            return false;
        }

        var expectedData = $"{transmissionId}|{transmissionTime}|{webhookId}|{ComputeCrc32(payload)}";
        var data = Encoding.UTF8.GetBytes(expectedData);

        if (!TryDecodeBase64(transmissionSig, out var signature))
        {
            return false;
        }

        using var rsa = CreateRsaFromPem(certificatePem);
        if (rsa == null)
        {
            return false;
        }

        return rsa.VerifyData(data, signature, hashAlgorithm, RSASignaturePadding.Pkcs1);
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

    private static bool TryGetHashAlgorithm(string authAlgo, out HashAlgorithmName hashAlgorithm)
    {
        hashAlgorithm = default;

        if (authAlgo.Equals("SHA256withRSA", StringComparison.OrdinalIgnoreCase))
        {
            hashAlgorithm = HashAlgorithmName.SHA256;
            return true;
        }

        if (authAlgo.Equals("SHA384withRSA", StringComparison.OrdinalIgnoreCase))
        {
            hashAlgorithm = HashAlgorithmName.SHA384;
            return true;
        }

        if (authAlgo.Equals("SHA512withRSA", StringComparison.OrdinalIgnoreCase))
        {
            hashAlgorithm = HashAlgorithmName.SHA512;
            return true;
        }

        return false;
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static RSA? CreateRsaFromPem(string pem)
    {
        try
        {
            if (pem.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal))
            {
                using var certificate = X509Certificate2.CreateFromPem(pem);
                var certificateKey = certificate.GetRSAPublicKey();
                return certificateKey == null ? null : RSA.Create(certificateKey.ExportParameters(false));
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch (CryptographicException)
        {
            return null;
        }
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
