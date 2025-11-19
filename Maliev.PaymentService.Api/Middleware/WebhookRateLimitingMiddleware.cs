using Maliev.PaymentService.Api.Models.Responses;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Maliev.PaymentService.Api.Middleware;

/// <summary>
/// Middleware for rate limiting webhook endpoints.
/// Applies 100 requests/minute limit per provider using distributed cache.
/// </summary>
public class WebhookRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<WebhookRateLimitingMiddleware> _logger;
    private const int MaxRequestsPerMinute = 100;
    private const int WindowSizeSeconds = 60;

    public WebhookRateLimitingMiddleware(
        RequestDelegate next,
        IDistributedCache cache,
        ILogger<WebhookRateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to webhook endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path == null || !path.StartsWith("/api/v1/webhooks/"))
        {
            await _next(context);
            return;
        }

        // Extract provider from path: /api/v1/webhooks/{provider}
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length < 4)
        {
            await _next(context);
            return;
        }

        var provider = pathSegments[3];
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"webhook_ratelimit:{provider}:{sourceIp}";

        try
        {
            // Get current request count
            var countData = await _cache.GetStringAsync(cacheKey);
            var currentCount = string.IsNullOrEmpty(countData) ? 0 : int.Parse(countData);

            if (currentCount >= MaxRequestsPerMinute)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for provider {Provider} from IP {SourceIp}. Count: {Count}",
                    provider, sourceIp, currentCount);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";

                var errorResponse = new ErrorResponse
                {
                    Error = "RATE_LIMIT_EXCEEDED",
                    Message = $"Rate limit exceeded. Maximum {MaxRequestsPerMinute} requests per minute allowed.",
                    Timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.Response.WriteAsync(json);
                return;
            }

            // Increment counter
            var newCount = currentCount + 1;
            await _cache.SetStringAsync(
                cacheKey,
                newCount.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(WindowSizeSeconds)
                });

            _logger.LogDebug(
                "Webhook request from provider {Provider}, IP {SourceIp}. Count: {Count}/{Max}",
                provider, sourceIp, newCount, MaxRequestsPerMinute);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in webhook rate limiting for provider {Provider}", provider);
            // Fail open - don't block requests if rate limiting fails
            await _next(context);
        }
    }
}
