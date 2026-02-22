namespace Maliev.PaymentService.Api.Clients;

/// <summary>
/// Client interface for uploading slip files to external storage service.
/// </summary>
public interface IUploadServiceClient
{
    /// <summary>
    /// Uploads a bank transfer slip image to cloud storage.
    /// </summary>
    /// <param name="fileStream">The file stream containing the slip image data.</param>
    /// <param name="fileName">The original file name of the uploaded file.</param>
    /// <param name="contentType">The MIME content type (e.g., image/jpeg, image/png).</param>
    /// <param name="paymentId">The unique identifier of the payment transaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The public URL of the uploaded slip image.</returns>
    /// <exception cref="HttpRequestException">Thrown when the upload service is unavailable.</exception>
    Task<string> UploadSlipAsync(Stream fileStream, string fileName, string contentType, Guid paymentId, CancellationToken ct);
}
