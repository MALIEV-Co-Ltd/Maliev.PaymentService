using Maliev.PaymentService.Api.Models.Responses;

namespace Maliev.PaymentService.Api.Clients;

/// <summary>
/// HTTP client implementation for analyzing bank transfer slips via the Chatbot Service.
/// Handles graceful degradation when the LLM service is unavailable.
/// </summary>
public class ChatbotServiceClient : IChatbotServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatbotServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the ChatbotServiceClient.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and resilience policies.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public ChatbotServiceClient(HttpClient httpClient, ILogger<ChatbotServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SlipAnalysisResult> AnalyzeSlipAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/chatbot/v1/vision/analyze-slip",
                new { imageUrl },
                ct);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SlipAnalysisResult>(cancellationToken: ct)
                ?? new SlipAnalysisResult { IsValid = false, Notes = "Empty response from verification service." };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Slip verification service unavailable for image {ImageUrl}", imageUrl);
            return new SlipAnalysisResult { IsValid = false, Notes = "Verification service unavailable." };
        }
    }
}
