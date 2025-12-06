using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models.Requests;

/// <summary>
/// Request to process a payment through the gateway.
/// </summary>
public class PaymentRequest : IValidatableObject
{
    /// <summary>
    /// Payment amount (must be greater than 0).
    /// </summary>
    [Range(0.01, 999999999.99, ErrorMessage = "Amount must be greater than 0 and less than 1,000,000,000")]
    public required decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "USD", "EUR", "GBP").
    /// Must be 3 uppercase characters.
    /// </summary>
    [Required(ErrorMessage = "Currency is required")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-letter ISO code")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase letters only (e.g., USD, EUR, THB)")]
    public required string Currency { get; set; }

    /// <summary>
    /// Customer identifier from the calling service.
    /// </summary>
    [Required(ErrorMessage = "CustomerId is required")]
    [StringLength(100, ErrorMessage = "CustomerId cannot exceed 100 characters")]
    public required string CustomerId { get; set; }

    /// <summary>
    /// Order/booking identifier from the calling service.
    /// </summary>
    [Required(ErrorMessage = "OrderId is required")]
    [StringLength(100, ErrorMessage = "OrderId cannot exceed 100 characters")]
    public required string OrderId { get; set; }

    /// <summary>
    /// Payment description.
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public required string Description { get; set; }

    /// <summary>
    /// URL to redirect user after successful payment.
    /// </summary>
    [Required(ErrorMessage = "ReturnUrl is required")]
    [Url(ErrorMessage = "ReturnUrl must be a valid URL")]
    public required string ReturnUrl { get; set; }

    /// <summary>
    /// URL to redirect user if payment is cancelled.
    /// </summary>
    [Required(ErrorMessage = "CancelUrl is required")]
    [Url(ErrorMessage = "CancelUrl must be a valid URL")]
    public required string CancelUrl { get; set; }

    /// <summary>
    /// Optional metadata for the payment (e.g., booking details, campaign info).
    /// Stored as JSONB in database.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Optional specific provider to use (overrides routing logic).
    /// If not specified, provider is selected based on currency and priority.
    /// </summary>
    [StringLength(50, ErrorMessage = "PreferredProvider cannot exceed 50 characters")]
    public string? PreferredProvider { get; set; }

    /// <summary>
    /// Performs custom validation for the payment request.
    /// </summary>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection of validation results.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && !ReturnUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("ReturnUrl must be a valid HTTPS URL", new[] { nameof(ReturnUrl) });
        }

        if (!string.IsNullOrWhiteSpace(CancelUrl) && !CancelUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("CancelUrl must be a valid HTTPS URL", new[] { nameof(CancelUrl) });
        }
    }
}
