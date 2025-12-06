using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Maliev.PaymentService.Infrastructure.Resilience;

/// <summary>
/// Resilience policies for provider communication using Microsoft.Extensions.Http.Resilience.
/// Configures retry, circuit breaker, and timeout policies per specification.
/// Uses the standard resilience handler with configurable settings.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Extension method to add standard resilience handler with payment service configuration.
    /// The standard handler includes retry with exponential backoff, circuit breaker, and timeout.
    /// Default ShouldHandle predicates already cover transient HTTP errors (5xx, timeouts, etc.).
    /// </summary>
    public static IHttpStandardResiliencePipelineBuilder AddPaymentResilienceHandler(
        this IHttpClientBuilder builder,
        IConfiguration configuration)
    {
        var retryCount = configuration.GetValue<int?>("Resilience:RetryCount") ?? 3;
        var timeoutSeconds = configuration.GetValue<int?>("Resilience:TimeoutSeconds") ?? 30;
        var failureThreshold = configuration.GetValue<double?>("Resilience:CircuitBreakerFailureThreshold") ?? 0.5;
        var samplingDuration = configuration.GetValue<int?>("Resilience:CircuitBreakerSamplingDurationSeconds") ?? 30;
        var breakDuration = configuration.GetValue<int?>("Resilience:CircuitBreakerBreakDurationSeconds") ?? 30;

        return builder.AddStandardResilienceHandler(options =>
        {
            // Retry configuration - uses exponential backoff by default
            options.Retry.MaxRetryAttempts = retryCount;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.UseJitter = true;

            // Circuit breaker configuration
            options.CircuitBreaker.FailureRatio = failureThreshold;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(samplingDuration);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(breakDuration);

            // Timeout configuration
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(timeoutSeconds * (retryCount + 1));
        });
    }
}
