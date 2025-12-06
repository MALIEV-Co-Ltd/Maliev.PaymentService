using System.Text.Json;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// API controller for receiving webhooks from payment providers.
/// Handles webhook validation, deduplication, and processing.
/// </summary>
[ApiController]
[Route("payments/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IProviderRepository _providerRepository;
    private readonly IWebhookRepository _webhookRepository;
    private readonly IWebhookValidationService _validationService;
    private readonly IWebhookProcessingService _processingService;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<WebhooksController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhooksController"/> class.
    /// </summary>
    /// <param name="providerRepository"></param>
    /// <param name="webhookRepository"></param>
    /// <param name="validationService"></param>
    /// <param name="processingService"></param>
    /// <param name="metricsService"></param>
    /// <param name="logger"></param>
    public WebhooksController(
        IProviderRepository providerRepository,
        IWebhookRepository webhookRepository,
        IWebhookValidationService validationService,
        IWebhookProcessingService processingService,
        IMetricsService metricsService,
        ILogger<WebhooksController> logger)
    {
        _providerRepository = providerRepository;
        _webhookRepository = webhookRepository;
        _validationService = validationService;
        _processingService = processingService;
        _metricsService = metricsService;
        _logger = logger;
    }

    /// <summary>
    /// Receives webhook notifications from payment providers.
    /// </summary>
    /// <param name="provider">The provider name (stripe, paypal, scb, omise).</param>
    /// <param name="payload">The webhook payload from the provider (JSON format).</param>
    /// <returns>Webhook received confirmation with event ID and processing status.</returns>
    /// <remarks>
    /// Receives and processes webhook notifications from payment providers. Webhooks are used by providers
    /// to notify the gateway about payment status changes (e.g., completed, failed, refunded).
    ///
    /// **Security:**
    /// - Signature validation using provider-specific secrets
    /// - Source IP validation (optional, provider-dependent)
    /// - Duplicate detection using event IDs
    /// - Rate limiting: 100 requests/minute per provider
    ///
    /// **Supported Providers:**
    /// - `stripe`: Stripe webhook events with Stripe-Signature header
    /// - `paypal`: PayPal IPN/webhooks with PayPal-Transmission-Sig header
    /// - `scb`: SCB API webhooks with X-SCB-Signature header
    /// - `omise`: Omise webhook events with Omise-Signature header
    ///
    /// **Webhook Processing:**
    /// 1. Signature validation (prevents tampering)
    /// 2. Duplicate detection (idempotent processing)
    /// 3. Event persistence to database
    /// 4. Asynchronous processing (background job)
    /// 5. Payment status update
    ///
    /// **Event Types:**
    /// - `payment.succeeded`: Payment completed successfully
    /// - `payment.failed`: Payment failed or declined
    /// - `payment.cancelled`: Payment cancelled by customer
    /// - `refund.succeeded`: Refund completed successfully
    /// - `refund.failed`: Refund failed
    ///
    /// **Response Time:**
    /// - Target: &lt; 200ms (fast acknowledgment)
    /// - Processing: Asynchronous (1-5 seconds)
    ///
    /// **Example Stripe Webhook:**
    /// ```bash
    /// POST /payments/v1/webhooks/stripe
    /// Stripe-Signature: t=1234567890,v1=abc123...
    /// Content-Type: application/json
    ///
    /// {
    ///   "id": "evt_123",
    ///   "type": "payment_intent.succeeded",
    ///   "data": {
    ///     "object": {
    ///       "id": "pi_123",
    ///       "amount": 9999,
    ///       "currency": "usd",
    ///       "status": "succeeded"
    ///     }
    ///   }
    /// }
    /// ```
    ///
    /// **Testing:**
    /// - Use provider test/sandbox webhooks for development
    /// - Stripe CLI: `stripe listen --forward-to localhost:5251/payments/v1/webhooks/stripe`
    /// - PayPal Sandbox: Configure webhook URL in PayPal Developer Dashboard
    /// </remarks>
    /// <response code="200">Webhook received and queued for processing. Returns event ID.</response>
    /// <response code="400">Invalid request. Unknown provider or malformed payload.</response>
    /// <response code="401">Unauthorized. Signature validation failed.</response>
    [HttpPost("{provider}")]
    [ProducesResponseType(typeof(WebhookReceivedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WebhookReceivedResponse>> ReceiveWebhook(
        string provider,
        [FromBody] JsonElement payload)
    {
        var startTime = DateTime.UtcNow;
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        _logger.LogInformation(
            "Received webhook from provider: {Provider}, IP: {SourceIp}",
            provider, sourceIp);

        try
        {
            // Get provider configuration
            var providerEntity = await _providerRepository.GetByNameAsync(provider);
            if (providerEntity == null)
            {
                _logger.LogWarning("Unknown provider: {Provider}", provider);
                return BadRequest(new ErrorResponse
                {
                    Error = "UNKNOWN_PROVIDER",
                    Message = $"Provider '{provider}' is not configured",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Read raw payload
            var rawPayload = payload.GetRawText();

            // Extract headers as dictionary
            var headers = Request.Headers.ToDictionary(
                h => h.Key,
                h => h.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);

            // Validate webhook signature
            var isValid = await _validationService.ValidateWebhookAsync(
                providerEntity,
                rawPayload,
                headers,
                sourceIp);

            if (!isValid)
            {
                _logger.LogWarning(
                    "Webhook signature validation failed for provider: {Provider}",
                    provider);

                _metricsService.RecordWebhookValidationFailure(provider);

                return Unauthorized(new ErrorResponse
                {
                    Error = "INVALID_SIGNATURE",
                    Message = "Webhook signature validation failed",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Extract provider event ID for deduplication
            var providerEventId = ExtractProviderEventId(payload, provider);

            if (string.IsNullOrWhiteSpace(providerEventId))
            {
                _logger.LogWarning(
                    "Could not extract event ID from webhook payload for provider: {Provider}",
                    provider);

                return BadRequest(new ErrorResponse
                {
                    Error = "MISSING_EVENT_ID",
                    Message = "Webhook payload missing required event ID",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check for duplicate
            var existing = await _webhookRepository.GetByProviderEventIdAsync(
                providerEntity.Id,
                providerEventId);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Duplicate webhook detected: {ProviderEventId} from provider {Provider}",
                    providerEventId, provider);

                return Ok(new WebhookReceivedResponse
                {
                    WebhookEventId = existing.Id,
                    Accepted = true,
                    IsDuplicate = true,
                    Message = "Webhook already processed",
                    ReceivedAt = existing.CreatedAt
                });
            }

            // Extract event type
            var eventType = ExtractEventType(payload, provider);

            // Create webhook event
            var webhookEvent = new WebhookEvent
            {
                Id = Guid.NewGuid(),
                ProviderId = providerEntity.Id,
                ProviderEventId = providerEventId,
                EventType = eventType,
                RawPayload = rawPayload,
                Signature = ExtractSignature(headers, provider),
                SignatureValidated = true,
                IpAddress = sourceIp,
                UserAgent = Request.Headers["User-Agent"].ToString(),
                ProcessingStatus = WebhookProcessingStatus.Pending,
                ProcessingAttempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RowVersion = Array.Empty<byte>()
            };

            // Save webhook event
            await _webhookRepository.AddAsync(webhookEvent);

            // Process webhook asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _processingService.ProcessWebhookAsync(webhookEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background webhook processing for {WebhookId}", webhookEvent.Id);
                }
            });

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _metricsService.RecordWebhookDuration(provider, duration);

            _logger.LogInformation(
                "Webhook {WebhookId} accepted and queued for processing",
                webhookEvent.Id);

            return Ok(new WebhookReceivedResponse
            {
                WebhookEventId = webhookEvent.Id,
                Accepted = true,
                IsDuplicate = false,
                Message = "Webhook received and queued for processing",
                ReceivedAt = webhookEvent.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook from provider: {Provider}", provider);

            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "An error occurred while processing the webhook",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Test endpoint for simulating webhooks in Development/Staging environments.
    /// Only available when not in Production.
    /// </summary>
    [HttpPost("{provider}/test")]
    [ProducesResponseType(typeof(WebhookReceivedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WebhookReceivedResponse>> TestWebhook(
        string provider,
        [FromBody] TestWebhookRequest request)
    {
        // Only allow in Development or Staging
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "NOT_ALLOWED",
                Message = "Test webhooks are not available in Production",
                Timestamp = DateTime.UtcNow
            });
        }

        _logger.LogInformation(
            "Test webhook requested for provider: {Provider}, event type: {EventType}",
            provider, request.EventType);

        // Get provider
        var providerEntity = await _providerRepository.GetByNameAsync(provider);
        if (providerEntity == null)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "UNKNOWN_PROVIDER",
                Message = $"Provider '{provider}' is not configured",
                Timestamp = DateTime.UtcNow
            });
        }

        // Create test payload
        var testPayload = new Dictionary<string, object>
        {
            { "id", $"test_{Guid.NewGuid():N}" },
            { "type", request.EventType },
            { "created", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "data", request.CustomData ?? new Dictionary<string, object>() }
        };

        if (request.TransactionId.HasValue)
        {
            testPayload["transactionId"] = request.TransactionId.Value;
        }

        var rawPayload = JsonSerializer.Serialize(testPayload);

        // Create webhook event
        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid(),
            ProviderId = providerEntity.Id,
            ProviderEventId = testPayload["id"].ToString()!,
            EventType = request.EventType,
            RawPayload = rawPayload,
            SignatureValidated = true, // Skip validation for test webhooks
            IpAddress = "127.0.0.1",
            UserAgent = "Test",
            ProcessingStatus = WebhookProcessingStatus.Pending,
            ProcessingAttempts = 0,
            PaymentTransactionId = request.TransactionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };

        await _webhookRepository.AddAsync(webhookEvent);

        // Process immediately for testing
        var result = await _processingService.ProcessWebhookAsync(webhookEvent);

        return Ok(new WebhookReceivedResponse
        {
            WebhookEventId = webhookEvent.Id,
            Accepted = true,
            IsDuplicate = false,
            Message = $"Test webhook processed. Success: {result.Success}",
            ReceivedAt = webhookEvent.CreatedAt
        });
    }

    private string ExtractProviderEventId(JsonElement payload, string provider)
    {
        // Try common field names
        var possibleKeys = new[] { "id", "event_id", "eventId", "webhookId", "webhook_id" };

        foreach (var key in possibleKeys)
        {
            if (payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var id = value.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        // Provider-specific extraction
        return provider.ToLowerInvariant() switch
        {
            "stripe" when payload.TryGetProperty("id", out var stripeId) => stripeId.GetString() ?? string.Empty,
            "paypal" when payload.TryGetProperty("id", out var paypalId) => paypalId.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private string ExtractEventType(JsonElement payload, string provider)
    {
        // Try common field names
        if (payload.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String)
        {
            return typeValue.GetString() ?? "unknown";
        }

        if (payload.TryGetProperty("event_type", out var eventTypeValue) && eventTypeValue.ValueKind == JsonValueKind.String)
        {
            return eventTypeValue.GetString() ?? "unknown";
        }

        if (payload.TryGetProperty("eventType", out var camelTypeValue) && camelTypeValue.ValueKind == JsonValueKind.String)
        {
            return camelTypeValue.GetString() ?? "unknown";
        }

        return "unknown";
    }

    private string? ExtractSignature(Dictionary<string, string> headers, string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "stripe" => headers.GetValueOrDefault("Stripe-Signature"),
            "paypal" => headers.GetValueOrDefault("PAYPAL-TRANSMISSION-SIG"),
            "omise" => headers.GetValueOrDefault("X-Omise-Signature"),
            "scb" => headers.GetValueOrDefault("X-SCB-Signature"),
            _ => null
        };
    }
}
