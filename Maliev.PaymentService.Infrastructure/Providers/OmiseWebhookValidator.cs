using System.Net;

namespace Maliev.PaymentService.Infrastructure.Providers;

/// <summary>
/// Validates Omise webhook requests using IP whitelist validation.
/// Omise sends webhooks from specific IP ranges that should be whitelisted.
/// </summary>
public class OmiseWebhookValidator
{
    // Omise production webhook IP ranges (as of 2024)
    // Note: These should be configurable and updated when Omise changes their IPs
    private static readonly HashSet<string> WhitelistedIpAddresses = new()
    {
        "52.74.115.100",
        "54.151.127.36",
        "54.169.162.201",
        "13.228.81.94",
        "18.141.73.155",
        "13.229.37.222"
    };

    // Omise IP ranges in CIDR notation
    private static readonly List<string> WhitelistedCidrRanges = new()
    {
        "52.74.0.0/16",
        "54.151.0.0/16",
        "13.228.0.0/16"
    };

    /// <summary>
    /// Validates an Omise webhook by checking if the source IP is whitelisted.
    /// </summary>
    /// <param name="sourceIp">Source IP address of the webhook request</param>
    /// <returns>True if IP is whitelisted</returns>
    public bool ValidateIpAddress(string? sourceIp)
    {
        if (string.IsNullOrWhiteSpace(sourceIp))
        {
            return false;
        }

        // Remove port if present
        var ipOnly = sourceIp.Split(':')[0];

        // Check exact IP match
        if (WhitelistedIpAddresses.Contains(ipOnly))
        {
            return true;
        }

        // Check CIDR range match
        if (!IPAddress.TryParse(ipOnly, out var ipAddress))
        {
            return false;
        }

        foreach (var cidr in WhitelistedCidrRanges)
        {
            if (IsIpInCidrRange(ipAddress, cidr))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Additional validation for Omise webhook using optional signature.
    /// Omise may include X-Omise-Signature header for additional security.
    /// </summary>
    public bool ValidateSignature(string payload, string? signature, string? secret)
    {
        // If no signature provided, rely on IP validation only
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
        {
            return true; // Skip signature validation if not configured
        }

        // Omise uses HMAC-SHA256 for optional signature validation
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToBase64String(computedHash);

        return signature.Equals(computedSignature, StringComparison.Ordinal);
    }

    private bool IsIpInCidrRange(IPAddress ipAddress, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var networkAddress) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var ipBytes = ipAddress.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();

        if (ipBytes.Length != networkBytes.Length)
        {
            return false;
        }

        var maskBytes = new byte[ipBytes.Length];
        for (int i = 0; i < maskBytes.Length; i++)
        {
            var bitsInByte = Math.Min(8, Math.Max(0, prefixLength - (i * 8)));
            maskBytes[i] = (byte)(0xFF << (8 - bitsInByte));
        }

        for (int i = 0; i < ipBytes.Length; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
