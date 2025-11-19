using Microsoft.Extensions.Configuration;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Maliev.PaymentService.Infrastructure.Resilience;

/// <summary>
/// Polly resilience policies for provider communication.
/// Implements retry, circuit breaker, and timeout policies per specification.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Creates a retry policy with exponential backoff.
    /// Retries up to 3 times with 30-second timeout per attempt.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateRetryPolicy(IConfiguration configuration)
    {
        var retryCount = configuration.GetValue<int?>("Polly:RetryCount") ?? 3;

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            })
            .Build();
    }

    /// <summary>
    /// Creates a circuit breaker policy.
    /// Triggers when EITHER condition is met first:
    /// - 5 consecutive failures OR
    /// - 50% failure rate over 30-second sliding window
    /// Break duration: 30 seconds
    /// Half-open state allows 1 test request
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateCircuitBreakerPolicy(IConfiguration configuration)
    {
        var failureThreshold = configuration.GetValue<double?>("Polly:CircuitBreakerFailureThreshold") ?? 0.5;
        var samplingDuration = configuration.GetValue<int?>("Polly:CircuitBreakerSamplingDurationSeconds") ?? 30;
        var breakDuration = configuration.GetValue<int?>("Polly:CircuitBreakerBreakDurationSeconds") ?? 30;

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = failureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(samplingDuration),
                MinimumThroughput = 5, // Minimum 5 requests before calculating failure rate
                BreakDuration = TimeSpan.FromSeconds(breakDuration),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            })
            .Build();
    }

    /// <summary>
    /// Creates a timeout policy.
    /// 30-second timeout per provider API call.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateTimeoutPolicy(IConfiguration configuration)
    {
        var timeoutSeconds = configuration.GetValue<int?>("Polly:TimeoutSeconds") ?? 30;

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TimeSpan.FromSeconds(timeoutSeconds))
            .Build();
    }

    /// <summary>
    /// Creates a combined resilience pipeline with timeout, retry, and circuit breaker.
    /// Execution order: Timeout -> Retry -> Circuit Breaker
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateCombinedPolicy(IConfiguration configuration)
    {
        var retryCount = configuration.GetValue<int?>("Polly:RetryCount") ?? 3;
        var timeoutSeconds = configuration.GetValue<int?>("Polly:TimeoutSeconds") ?? 30;
        var failureThreshold = configuration.GetValue<double?>("Polly:CircuitBreakerFailureThreshold") ?? 0.5;
        var samplingDuration = configuration.GetValue<int?>("Polly:CircuitBreakerSamplingDurationSeconds") ?? 30;
        var breakDuration = configuration.GetValue<int?>("Polly:CircuitBreakerBreakDurationSeconds") ?? 30;

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Timeout policy (innermost)
            .AddTimeout(TimeSpan.FromSeconds(timeoutSeconds))
            // Retry policy (middle)
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = retryCount,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            })
            // Circuit breaker policy (outermost)
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = failureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(samplingDuration),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(breakDuration),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            })
            .Build();
    }
}
