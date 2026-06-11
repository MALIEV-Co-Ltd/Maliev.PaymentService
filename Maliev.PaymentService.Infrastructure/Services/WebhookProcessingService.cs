using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

            if (existing != null && existing.Id != webhookEvent.Id)
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
                // Update transaction status based on webhook event. The webhook row is linked
                // only when the transaction exists so unmatched provider callbacks remain
                // persistable for retry/inspection without violating the FK constraint.
                var linked = await UpdateTransactionStatusAsync(
                    transactionId.Value,
                    webhookEvent.EventType,
                    parsedData,
                    webhookEvent.CorrelationId,
                    cancellationToken);
                if (linked)
                {
                    webhookEvent.PaymentTransactionId = transactionId.Value;
                }
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
        var webhookEvent = await _webhookRepository.GetByIdAsync(webhookEventId, cancellationToken);

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

        if (TryExtractGuidFromMetadata(parsedData, out var metadataTransactionId))
        {
            return metadataTransactionId;
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

    private static bool TryExtractGuidFromMetadata(Dictionary<string, object> parsedData, out Guid transactionId)
    {
        transactionId = default;

        if (parsedData.TryGetValue("metadata", out var metadata) &&
            TryExtractGuidFromJsonObject(metadata, out transactionId))
        {
            return true;
        }

        if (parsedData.TryGetValue("data", out var data) &&
            data is JsonElement dataElement &&
            dataElement.ValueKind == JsonValueKind.Object &&
            dataElement.TryGetProperty("object", out var objectElement) &&
            objectElement.ValueKind == JsonValueKind.Object &&
            objectElement.TryGetProperty("metadata", out var nestedMetadata))
        {
            return TryExtractGuidFromJsonObject(nestedMetadata, out transactionId);
        }

        return false;
    }

    private static bool TryExtractGuidFromJsonObject(object value, out Guid transactionId)
    {
        transactionId = default;

        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if ((element.TryGetProperty("transactionId", out var transactionIdProperty) ||
             element.TryGetProperty("transaction_id", out transactionIdProperty) ||
             element.TryGetProperty("paymentId", out transactionIdProperty) ||
             element.TryGetProperty("payment_id", out transactionIdProperty)) &&
            transactionIdProperty.ValueKind == JsonValueKind.String &&
            Guid.TryParse(transactionIdProperty.GetString(), out transactionId))
        {
            return true;
        }

        return false;
    }

    private async Task<bool> UpdateTransactionStatusAsync(
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
            return false;
        }

        var previousStatus = transaction.Status;

        // Map event type to payment status
        var newStatus = MapEventTypeToStatus(eventType);

        if (newStatus == transaction.Status)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} already in status {Status}, skipping update",
                transactionId, newStatus);
            return true;
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
            var payload = new PaymentCompletedEventPayload(
                OrderId: Guid.TryParse(transaction.OrderId, out var orderId) ? orderId : Guid.Empty,
                OrderNumber: ResolveOrderNumber(transaction),
                CustomerId: transaction.CustomerId,
                PaymentId: transaction.Id,
                Amount: (double)transaction.Amount,
                Currency: transaction.Currency
            );

            var publicEvent = new PaymentCompletedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PaymentCompletedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "PaymentService",
                ConsumedBy: new[] { "InvoiceService", "OrderService", "NotificationService" },
                CorrelationId: Guid.TryParse(transaction.CorrelationId, out var correlId) ? correlId : Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: payload
            );

            await _eventPublisher.PublishAsync(publicEvent, cancellationToken);
        }
        else if (newStatus == PaymentStatus.Failed)
        {
            var failedEvent = new PaymentFailedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: nameof(PaymentFailedEvent),
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "PaymentService",
                ConsumedBy: new[] { "NotificationService" },
                CorrelationId: Guid.TryParse(transaction.CorrelationId, out var correlId) ? correlId : Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentFailedEventPayload(
                    TransactionId: transaction.Id,
                    IdempotencyKey: transaction.IdempotencyKey,
                    Amount: (double)transaction.Amount,
                    Currency: transaction.Currency,
                    CustomerId: transaction.CustomerId,
                    OrderId: transaction.OrderId,
                    ProviderName: transaction.ProviderName,
                    ErrorMessage: $"Payment failed via webhook: {eventType}",
                    ProviderErrorCode: eventType,
                    FailedAt: DateTimeOffset.UtcNow
                )
            );

            await _eventPublisher.PublishAsync(failedEvent, cancellationToken);
        }

        _logger.LogInformation(
            "Transaction {TransactionId} status updated from {PreviousStatus} to {NewStatus} via webhook",
            transactionId, previousStatus, newStatus);
        return true;
    }

    private static string ResolveOrderNumber(PaymentTransaction transaction)
    {
        if (transaction.Metadata != null &&
            (transaction.Metadata.TryGetValue("orderNumber", out var orderNumber) ||
             transaction.Metadata.TryGetValue("OrderNumber", out orderNumber)) &&
            !string.IsNullOrWhiteSpace(orderNumber))
        {
            return orderNumber;
        }

        return string.IsNullOrWhiteSpace(transaction.OrderId) ? "Unknown" : transaction.OrderId;
    }

    private PaymentStatus MapEventTypeToStatus(string eventType)
    {
        // Normalize event type
        var normalized = eventType.ToLowerInvariant().Replace(".", "_").Replace("-", "_");

        return normalized switch
        {
            var e when e.Contains("completed") || e.Contains("succeeded") || e.Contains("success") => PaymentStatus.Completed,
            var e when e.Contains("failed") || e.Contains("failure") || e.Contains("declined") || e.Contains("cancelled") || e.Contains("canceled") || e.Contains("expired") => PaymentStatus.Failed,
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
