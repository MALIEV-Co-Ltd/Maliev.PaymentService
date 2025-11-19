using Maliev.PaymentService.Core.Constants;
using System.Security.Claims;

namespace Maliev.PaymentService.Api.Middleware;

/// <summary>
/// Middleware for validating service identity claims in JWT tokens.
/// Ensures requests are from authenticated internal services with required claims.
/// </summary>
public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;

    public JwtAuthenticationMiddleware(RequestDelegate next, ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health check endpoints
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Skip authentication for webhook endpoints (authenticated differently)
        if (context.Request.Path.StartsWithSegments("/v1/webhooks"))
        {
            await _next(context);
            return;
        }

        // Skip authentication for metrics endpoint
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        // Verify user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Unauthenticated request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Authentication required for this endpoint"
            });
            return;
        }

        // Validate service identity claims
        var serviceId = context.User.FindFirst(AuthConstants.ClaimTypes.ServiceId)?.Value;
        var serviceName = context.User.FindFirst(AuthConstants.ClaimTypes.ServiceName)?.Value;

        if (string.IsNullOrEmpty(serviceId))
        {
            _logger.LogWarning("Missing service_id claim for authenticated request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Forbidden",
                message = "Service identity claims required"
            });
            return;
        }

        // Log service identity for audit trail
        _logger.LogInformation("Request from service {ServiceId} ({ServiceName}) to {Path}",
            serviceId, serviceName ?? "unknown", context.Request.Path);

        // Add service identity to HttpContext.Items for downstream access
        context.Items["ServiceId"] = serviceId;
        context.Items["ServiceName"] = serviceName ?? "unknown";

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering JwtAuthenticationMiddleware.
/// </summary>
public static class JwtAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuthenticationMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<JwtAuthenticationMiddleware>();
    }
}
