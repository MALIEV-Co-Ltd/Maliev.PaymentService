using System.Net.Http.Headers;

namespace Maliev.PaymentService.Api.Clients;

/// <summary>
/// HTTP client implementation for uploading slip files to the external upload service.
/// </summary>
public class UploadServiceClient : IUploadServiceClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the UploadServiceClient.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and resilience policies.</param>
    public UploadServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<string> UploadSlipAsync(Stream fileStream, string fileName, string contentType, Guid paymentId, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent("payment-service"), "ServiceName");
        content.Add(new StringContent("payment-slips"), "Path");

        var response = await _httpClient.PostAsync("/upload/v1/uploads", content, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>(cancellationToken: ct);
        return result?.PublicUrl ?? throw new InvalidOperationException("Failed to extract URL from upload response.");
    }

    /// <summary>
    /// Internal response model for upload service API.
    /// </summary>
    private class UploadResponse
    {
        /// <summary>
        /// Gets or sets the public URL of the uploaded file.
        /// </summary>
        public string PublicUrl { get; set; } = string.Empty;
    }
}
