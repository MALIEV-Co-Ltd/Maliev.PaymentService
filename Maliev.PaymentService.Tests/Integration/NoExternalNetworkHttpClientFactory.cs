namespace Maliev.PaymentService.Tests.Integration;

/// <summary>
/// Creates clients that fail closed if an integration test attempts outbound HTTP.
/// </summary>
internal sealed class NoExternalNetworkHttpClientFactory : IHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(new RejectOutboundHttpHandler(), disposeHandler: true);

    private sealed class RejectOutboundHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"Integration tests must not call external payment providers: {request.Method} {request.RequestUri}");
    }
}
