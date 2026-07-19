using System.Text.Json.Serialization;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Single payment row returned by paged payment endpoints.
/// </summary>
public class PaymentSummaryResponse
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }

    /// <summary>
    /// Human-readable payment reference.
    /// </summary>
    [JsonPropertyName("paymentNumber")]
    public required string PaymentNumber { get; set; }

    /// <summary>
    /// Invoice reference linked to this payment.
    /// </summary>
    [JsonPropertyName("invoiceNumber")]
    public required string InvoiceNumber { get; set; }

    /// <summary>
    /// Customer display value used by finance screens.
    /// </summary>
    [JsonPropertyName("customerName")]
    public required string CustomerName { get; set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    [JsonPropertyName("amount")]
    public required decimal Amount { get; set; }

    /// <summary>
    /// Friendly method name.
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; set; }

    /// <summary>
    /// Canonical provider method code.
    /// </summary>
    [JsonPropertyName("paymentMethod")]
    public required string PaymentMethod { get; set; }

    /// <summary>
    /// Payment status value.
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp that payment was finalized or last updated payment moment.
    /// </summary>
    [JsonPropertyName("paymentDate")]
    public required DateTime PaymentDate { get; set; }
}

/// <summary>
/// Pagination metadata included in list responses.
/// </summary>
public class PaginationMeta
{
    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    /// <summary>
    /// Total number of matching rows.
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    /// <summary>
    /// Count returned in the current response.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Requested page size.
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

/// <summary>
/// Generic paged response wrapper.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// Rows for the current page.
    /// </summary>
    [JsonPropertyName("data")]
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// Pagination metadata.
    /// </summary>
    [JsonPropertyName("meta")]
    public PaginationMeta Meta { get; set; } = new();
}
