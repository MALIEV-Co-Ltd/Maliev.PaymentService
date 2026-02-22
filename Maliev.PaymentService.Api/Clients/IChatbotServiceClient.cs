using Maliev.PaymentService.Api.Models.Responses;

namespace Maliev.PaymentService.Api.Clients;

/// <summary>
/// Client interface for analyzing bank transfer slips using LLM vision capabilities.
/// </summary>
public interface IChatbotServiceClient
{
    /// <summary>
    /// Analyzes a bank transfer slip image to extract payment verification data.
    /// </summary>
    /// <param name="imageUrl">The public URL of the slip image to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A slip analysis result containing extracted amount, bank name, and validity status.</returns>
    /// <exception cref="HttpRequestException">Thrown when the chatbot service is unavailable.</exception>
    Task<SlipAnalysisResult> AnalyzeSlipAsync(string imageUrl, CancellationToken ct);
}
