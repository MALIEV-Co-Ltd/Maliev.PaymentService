using System.Net;
using System.Text.Json;

namespace Maliev.PaymentService.Api.Middleware;

/// <summary>
/// Middleware for global exception handling with standardized error responses.
/// Catches unhandled exceptions and returns consistent error format.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";

        _logger.LogError(exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}",
            correlationId, context.Request.Path);

        var (statusCode, error, message) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "InvalidArgument", exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "InvalidArgument", exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, "InvalidOperation", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Forbidden", "Access denied"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NotFound", "Resource not found"),
            _ => (HttpStatusCode.InternalServerError, "InternalServerError", "An unexpected error occurred")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var errorResponse = new
        {
            error,
            message,
            correlationId,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.ToString(),
            // Only include stack trace in development
            stackTrace = _environment.IsDevelopment() ? exception.StackTrace : null
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsJsonAsync(errorResponse, jsonOptions);
    }
}

/// <summary>
/// Extension methods for registering ExceptionHandlingMiddleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
