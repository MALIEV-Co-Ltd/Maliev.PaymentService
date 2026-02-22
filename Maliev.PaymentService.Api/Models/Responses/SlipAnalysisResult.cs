using System.Text.Json.Serialization;

namespace Maliev.PaymentService.Api.Models.Responses;

/// <summary>
/// Response from the ChatbotService LLM vision analysis for bank transfer slips.
/// </summary>
public class SlipAnalysisResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the slip image was successfully parsed and verified.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the payment amount extracted from the slip in Thai Baht.
    /// </summary>
    [JsonPropertyName("extractedAmountThb")]
    public decimal? ExtractedAmountThb { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank identified on the slip.
    /// </summary>
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    /// <summary>
    /// Gets or sets the transfer date shown on the slip.
    /// </summary>
    [JsonPropertyName("transferDate")]
    public string? TransferDate { get; set; }

    /// <summary>
    /// Gets or sets additional notes or error messages from the verification process.
    /// </summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
