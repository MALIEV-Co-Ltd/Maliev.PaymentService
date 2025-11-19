namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Standardized error response for API errors.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code (e.g., "INVALID_CURRENCY", "IDEMPOTENCY_KEY_REQUIRED").
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Additional error details (optional).
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Correlation ID for tracking the request (optional).
    /// </summary>
    public string? CorrelationId { get; init; }
}
