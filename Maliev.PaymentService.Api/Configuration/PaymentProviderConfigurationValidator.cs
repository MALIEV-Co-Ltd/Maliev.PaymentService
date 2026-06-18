using Microsoft.Extensions.Configuration;

namespace Maliev.PaymentService.Api.Configuration;

/// <summary>
/// Validates payment provider configuration required for production startup.
/// </summary>
public static class PaymentProviderConfigurationValidator
{
    private const string OmiseSectionName = "PaymentProviders:Omise";
    private const string StripeSectionName = "PaymentProviders:Stripe";

    private static readonly Dictionary<string, string> OmiseDevelopmentPlaceholders = new(StringComparer.Ordinal)
    {
        ["PublicKey"] = "local-placeholder-omise-public-key",
        ["SecretKey"] = "local-placeholder-omise-secret-key",
        ["WebhookSecret"] = "local-placeholder-omise-webhook-secret"
    };

    private static readonly Dictionary<string, string> StripeDevelopmentPlaceholders = new(StringComparer.Ordinal)
    {
        ["ApiKey"] = "local-placeholder-stripe-api-key",
        ["WebhookSecret"] = "local-placeholder-stripe-webhook-secret"
    };

    /// <summary>
    /// Validates that deployed Omise configuration is explicitly supplied and does not use local placeholders.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environmentName">The current host environment name.</param>
    /// <exception cref="InvalidOperationException">Thrown when deployed Omise configuration is missing or unsafe.</exception>
    public static void ValidateOmiseForEnvironment(IConfiguration configuration, string environmentName)
    {
        ValidateProviderForEnvironment(
            configuration,
            environmentName,
            OmiseSectionName,
            ["PublicKey", "SecretKey", "WebhookSecret", "ApiBaseUrl"],
            OmiseDevelopmentPlaceholders);
    }

    /// <summary>
    /// Validates that deployed Stripe configuration is explicitly supplied and does not use local placeholders.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environmentName">The current host environment name.</param>
    /// <exception cref="InvalidOperationException">Thrown when deployed Stripe configuration is missing or unsafe.</exception>
    public static void ValidateStripeForEnvironment(IConfiguration configuration, string environmentName)
    {
        ValidateProviderForEnvironment(
            configuration,
            environmentName,
            StripeSectionName,
            ["ApiKey", "WebhookSecret", "ApiBaseUrl"],
            StripeDevelopmentPlaceholders);
    }

    private static void ValidateProviderForEnvironment(
        IConfiguration configuration,
        string environmentName,
        string sectionName,
        IReadOnlyCollection<string> requiredKeys,
        IReadOnlyDictionary<string, string> developmentPlaceholders)
    {
        if (IsLocalEnvironment(environmentName))
        {
            return;
        }

        var providerSection = configuration.GetSection(sectionName);
        var invalidKeys = new List<string>();

        foreach (var requiredKey in requiredKeys)
        {
            if (string.IsNullOrWhiteSpace(providerSection[requiredKey]))
            {
                invalidKeys.Add($"{sectionName}:{requiredKey} is missing");
            }
        }

        foreach (var placeholder in developmentPlaceholders)
        {
            if (string.Equals(providerSection[placeholder.Key], placeholder.Value, StringComparison.Ordinal))
            {
                invalidKeys.Add($"{sectionName}:{placeholder.Key} uses a development placeholder");
            }
        }

        if (invalidKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Deployed {sectionName} payment provider configuration is invalid: " + string.Join("; ", invalidKeys));
        }
    }

    private static bool IsLocalEnvironment(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
