using System.Text.Json;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Service for processing webhook events from payment providers.
/// Handles deduplication, transaction updates, and event publishing.
/// </summary>
public class WebhookProcessingService : IWebhookProcessingService
{
    private readonly IWebhookRepository _webhookRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<WebhookProcessingService> _logger;

    public WebhookProcessingService(
        IWebhookRepository webhookRepository,
        IPaymentRepository paymentRepository,
        IEventPublisher eventPublisher,
        IMetricsService metricsService,
        ILogger<WebhookProcessingService> logger)
    {
        _webhookRepository = webhookRepository;
        _paymentRepository = paymentRepository;
        _eventPublisher = eventPublisher;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<WebhookProcessingResult> ProcessWebhookAsync(
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing webhook {WebhookId} from provider {ProviderId}, event type: {EventType}",
            webhookEvent.Id, webhookEvent.ProviderId, webhookEvent.EventType);

        try
        {
            // Check for duplicate
            var existing = await _webhookRepository.GetByProviderEventIdAsync(
                webhookEvent.ProviderId,
                webhookEvent.ProviderEventId,
                cancellationToken);

            if (existing != null)
            {
                _logger.LogInformation(
                    "Duplicate webhook detected: {ProviderEventId} from provider {ProviderId}",
                    webhookEvent.ProviderEventId, webhookEvent.ProviderId);

                webhookEvent.ProcessingStatus = WebhookProcessingStatus.Duplicate;
                await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken);

                _metricsService.RecordWebhookProcessed(
                    webhookEvent.PaymentProvider?.Name ?? "unknown",
                    webhookEvent.EventType,
                    false);

                return new WebhookProcessingResult
                {
                    Success = true,
                    IsDuplicate = true
                };
            }

            // Update status to processing
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Processing;
            webhookEvent.ProcessingAttempts++;
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken);

            // Parse webhook payload
            var parsedData = ParseWebhookPayload(webhookEvent.RawPayload);
            webhookEvent.ParsedPayload = parsedData;

            // Extract transaction ID from webhook
            var transactionId = ExtractTransactionId(webhookEvent.EventType, parsedData, webhookEvent.PaymentProvider?.Name);

            if (transactionId.HasValue)
            {
                webhookEvent.PaymentTransactionId = transactionId.Value;

                // Update transaction status based on webhook event
                await UpdateTransactionStatusAsync(
                    transactionId.Value,
                    webhookEvent.EventType,
                    parsedData,
                    webhookEvent.CorrelationId,
                    cancellationToken);
            }

            // Mark as completed
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Completed;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken);

            _logger.LogInformation(
                "Webhook {WebhookId} processed successfully",
                webhookEvent.Id);

            _metricsService.RecordWebhookProcessed(
                webhookEvent.PaymentProvider?.Name ?? "unknown",
                webhookEvent.EventType,
                true);

            return new WebhookProcessingResult
            {
                Success = true,
                IsDuplicate = false,
                TransactionId = transactionId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing webhook {WebhookId}: {ErrorMessage}",
                webhookEvent.Id, ex.Message);

            // Mark as failed and schedule retry
            webhookEvent.ProcessingStatus = WebhookProcessingStatus.Failed;
            webhookEvent.FailedAt = DateTime.UtcNow;
            webhookEvent.FailureReason = ex.Message;
            webhookEvent.NextRetryAt = CalculateNextRetryTime(webhookEvent.ProcessingAttempts);

            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken);

            _metricsService.RecordWebhookProcessed(
                webhookEvent.PaymentProvider?.Name ?? "unknown",
                webhookEvent.EventType,
                false);

            return new WebhookProcessingResult
            {
                Success = false,
                IsDuplicate = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<WebhookProcessingResult> RetryWebhookAsync(
        Guid webhookEventId,
        CancellationToken cancellationToken = default)
    {
        var webhookEvent = await _webhookRepository.GetByProviderEventIdAsync(Guid.Empty, webhookEventId.ToString(), cancellationToken);

        if (webhookEvent == null)
        {
            return new WebhookProcessingResult
            {
                Success = false,
                IsDuplicate = false,
                ErrorMessage = "Webhook event not found"
            };
        }

        return await ProcessWebhookAsync(webhookEvent, cancellationToken);
    }

    private Dictionary<string, object>? ParseWebhookPayload(string rawPayload)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(rawPayload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse webhook payload as JSON");
            return null;
        }
    }

    private Guid? ExtractTransactionId(string eventType, Dictionary<string, object>? parsedData, string? providerName)
    {
        if (parsedData == null)
        {
            return null;
        }

        // Try common field names for transaction ID
        var possibleKeys = new[] { "transactionId", "transaction_id", "paymentId", "payment_id", "id", "metadata" };

        foreach (var key in possibleKeys)
        {
            if (parsedData.TryGetValue(key, out var value))
            {
                // If it's metadata, look inside
                if (key == "metadata" && value is JsonElement metadataElement && metadataElement.ValueKind == JsonValueKind.Object)
                {
                    if (metadataElement.TryGetProperty("transactionId", out var txIdProp) ||
                        metadataElement.TryGetProperty("transaction_id", out txIdProp))
                    {
                        if (Guid.TryParse(txIdProp.GetString(), out var txId))
                        {
                            return txId;
                        }
                    }
                }

                // Try direct parsing
                var valueStr = value?.ToString();
                if (!string.IsNullOrWhiteSpace(valueStr) && Guid.TryParse(valueStr, out var transactionId))
                {
                    return transactionId;
                }
            }
        }

        _logger.LogWarning(
            "Could not extract transaction ID from webhook event type {EventType} from provider {ProviderName}",
            eventType, providerName);

        return null;
    }

    private async Task UpdateTransactionStatusAsync(
        Guid transactionId,
        string eventType,
        Dictionary<string, object>? parsedData,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        var transaction = await _paymentRepository.GetByIdAsync(transactionId, cancellationToken);

        if (transaction == null)
        {
            _logger.LogWarning("Transaction {TransactionId} not found for webhook event", transactionId);
            return;
        }

        var previousStatus = transaction.Status;

        // Map event type to payment status
        var newStatus = MapEventTypeToStatus(eventType);

        if (newStatus == transaction.Status)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} already in status {Status}, skipping update",
                transactionId, newStatus);
            return;
        }

        transaction.Status = newStatus;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (newStatus == PaymentStatus.Completed)
        {
            transaction.CompletedAt = DateTime.UtcNow;
        }

        await _paymentRepository.UpdateAsync(transaction, cancellationToken);

        // Add transaction log
        await _paymentRepository.AddLogAsync(new TransactionLog
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transactionId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            EventType = $"Webhook:{eventType}",
            Message = $"Status updated via webhook: {eventType}",
            CorrelationId = correlationId?.ToString() ?? transaction.CorrelationId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // Publish event if payment completed
        if (newStatus == PaymentStatus.Completed)
        {
            await _eventPublisher.PublishAsync(new Core.Events.PaymentCompletedEvent
            {
                TransactionId = transaction.Id,
                IdempotencyKey = transaction.IdempotencyKey,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                CustomerId = transaction.CustomerId,
                OrderId = transaction.OrderId,
                ProviderName = transaction.ProviderName,
                ProviderTransactionId = transaction.ProviderTransactionId,
                CompletedAt = transaction.CompletedAt ?? DateTime.UtcNow,
                CorrelationId = transaction.CorrelationId
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Transaction {TransactionId} status updated from {PreviousStatus} to {NewStatus} via webhook",
            transactionId, previousStatus, newStatus);
    }

    private PaymentStatus MapEventTypeToStatus(string eventType)
    {
        // Normalize event type
        var normalized = eventType.ToLowerInvariant().Replace(".", "_").Replace("-", "_");

        return normalized switch
        {
            var e when e.Contains("completed") || e.Contains("succeeded") || e.Contains("success") => PaymentStatus.Completed,
            var e when e.Contains("failed") || e.Contains("failure") || e.Contains("declined") || e.Contains("cancelled") || e.Contains("canceled") => PaymentStatus.Failed,
            var e when e.Contains("pending") || e.Contains("processing") => PaymentStatus.Processing,
            var e when e.Contains("refunded") => PaymentStatus.Refunded,
            _ => PaymentStatus.Processing // Default to processing for unknown events
        };
    }

    private DateTime CalculateNextRetryTime(int attemptNumber)
    {
        // Exponential backoff: 1 min, 5 min, 15 min, 1 hour, 6 hours
        var delayMinutes = attemptNumber switch
        {
            1 => 1,
            2 => 5,
            3 => 15,
            4 => 60,
            _ => 360
        };

        return DateTime.UtcNow.AddMinutes(delayMinutes);
    }
}
