namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response for slip upload API.
/// </summary>
public class SlipUploadResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the payment transaction.
    /// </summary>
    public Guid TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the payment (e.g., Completed, PendingVerification).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public URL where the uploaded slip image is stored.
    /// </summary>
    public string SlipUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the slip was automatically verified by the LLM service.
    /// </summary>
    public bool AutoVerified { get; set; }

    /// <summary>
    /// Gets or sets the payment amount extracted from the slip in Thai Baht, if available.
    /// </summary>
    public decimal? ExtractedAmountThb { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the upload result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
