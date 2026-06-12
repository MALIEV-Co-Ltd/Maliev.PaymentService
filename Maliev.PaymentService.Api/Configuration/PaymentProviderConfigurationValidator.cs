using Microsoft.Extensions.Configuration;

namespace Maliev.PaymentService.Api.Configuration;

/// <summary>
/// Validates payment provider configuration required for production startup.
/// </summary>
public static class PaymentProviderConfigurationValidator
{
    private const string OmiseSectionName = "PaymentProviders:Omise";

    private static readonly Dictionary<string, string> DevelopmentPlaceholders = new(StringComparer.Ordinal)
    {
        ["PublicKey"] = "pkey_test_development_omise_key",
        ["SecretKey"] = "skey_test_development_omise_key",
        ["WebhookSecret"] = "whsec_omise_development_secret"
    };

    /// <summary>
    /// Validates that production Omise configuration is explicitly supplied and does not use local placeholders.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environmentName">The current host environment name.</param>
    /// <exception cref="InvalidOperationException">Thrown when production Omise configuration is missing or unsafe.</exception>
    public static void ValidateOmiseForEnvironment(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var omiseSection = configuration.GetSection(OmiseSectionName);
        var invalidKeys = new List<string>();

        foreach (var requiredKey in new[] { "PublicKey", "SecretKey", "WebhookSecret", "ApiBaseUrl" })
        {
            if (string.IsNullOrWhiteSpace(omiseSection[requiredKey]))
            {
                invalidKeys.Add($"{OmiseSectionName}:{requiredKey} is missing");
            }
        }

        foreach (var placeholder in DevelopmentPlaceholders)
        {
            if (string.Equals(omiseSection[placeholder.Key], placeholder.Value, StringComparison.Ordinal))
            {
                invalidKeys.Add($"{OmiseSectionName}:{placeholder.Key} uses a development placeholder");
            }
        }

        if (invalidKeys.Count > 0)
        {
            throw new InvalidOperationException(
                "Production Omise payment provider configuration is invalid: " + string.Join("; ", invalidKeys));
        }
    }
}
