using System.Diagnostics;

namespace Maliev.PaymentService.Api.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses with structured JSON logging.
/// Uses Serilog ILogger configured in Program.cs, logs request/response details with correlation IDs.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to log incoming HTTP requests and outgoing HTTP responses.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";

        // Log request
        _logger.LogInformation(
            "HTTP {Method} {Path} started. CorrelationId: {CorrelationId}, ContentType: {ContentType}, ContentLength: {ContentLength}",
            context.Request.Method,
            context.Request.Path,
            correlationId,
            context.Request.ContentType ?? "none",
            context.Request.ContentLength ?? 0);

        // Execute the request
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            var logLevel = context.Response.StatusCode >= 500
                ? LogLevel.Error
                : context.Response.StatusCode >= 400
                    ? LogLevel.Warning
                    : LogLevel.Information;

            _logger.Log(logLevel,
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId);
        }
    }
}

/// <summary>
/// Extension methods for registering RequestLoggingMiddleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="RequestLoggingMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
