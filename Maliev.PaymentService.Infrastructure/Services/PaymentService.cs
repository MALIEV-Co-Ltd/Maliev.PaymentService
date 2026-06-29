using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Providers;
using Maliev.PaymentService.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Payment orchestration service.
/// Coordinates idempotency, routing, provider calls, resilience, and event publishing.
/// </summary>
public class PaymentService : IPaymentService
{
    private static readonly string[] PaymentFailedConsumers =
        ["OrderService", "NotificationService", "QuoteEngine"];

    private static readonly string[] PaymentPendingConsumers =
        ["OrderService", "NotificationService", "QuoteEngine"];

    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentRoutingService _routingService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMetricsService _metricsService;
    private readonly ProviderFactory _providerFactory;
    private readonly CircuitBreakerStateManager _circuitBreakerStateManager;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IPaymentRoutingService routingService,
        IIdempotencyService idempotencyService,
        IEventPublisher eventPublisher,
        IMetricsService metricsService,
        ProviderFactory providerFactory,
        CircuitBreakerStateManager circuitBreakerStateManager,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _routingService = routingService;
        _idempotencyService = idempotencyService;
        _eventPublisher = eventPublisher;
        _metricsService = metricsService;
        _providerFactory = providerFactory;
        _circuitBreakerStateManager = circuitBreakerStateManager;
        _logger = logger;
    }

    public async Task<PaymentTransaction> ProcessPaymentAsync(PaymentProcessingRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Check idempotency - return existing transaction if already processed
        var existingTransaction = await _paymentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existingTransaction != null)
        {
            _logger.LogInformation("Idempotent request detected. Returning existing transaction {TransactionId}",
                existingTransaction.Id);
            request.ExistingTransactionReturned = true;
            return existingTransaction;
        }

        var completedOrderPayment = await _paymentRepository.GetLatestCompletedByOrderIdAsync(
            request.OrderId,
            cancellationToken);
        if (completedOrderPayment != null)
        {
            _logger.LogWarning(
                "Order {OrderId} already has completed payment {TransactionId}. Returning existing transaction for idempotency key {IdempotencyKey}",
                request.OrderId,
                completedOrderPayment.Id,
                request.IdempotencyKey);
            request.ExistingTransactionReturned = true;
            return completedOrderPayment;
        }

        // Acquire distributed lock for idempotency key
        var lockAcquired = await _idempotencyService.AcquireLockAsync(
            "payment",
            request.IdempotencyKey,
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogWarning("Failed to acquire lock for idempotency key {IdempotencyKey}", request.IdempotencyKey);
            throw new InvalidOperationException("Unable to process payment at this time. Please retry.");
        }

        try
        {
            // Double-check idempotency after acquiring lock
            existingTransaction = await _paymentRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existingTransaction != null)
            {
                _logger.LogInformation("Idempotent request detected after lock acquisition. Returning existing transaction {TransactionId}",
                    existingTransaction.Id);
                request.ExistingTransactionReturned = true;
                return existingTransaction;
            }

            completedOrderPayment = await _paymentRepository.GetLatestCompletedByOrderIdAsync(
                request.OrderId,
                cancellationToken);
            if (completedOrderPayment != null)
            {
                _logger.LogWarning(
                    "Order {OrderId} already has completed payment {TransactionId} after lock acquisition. Returning existing transaction for idempotency key {IdempotencyKey}",
                    request.OrderId,
                    completedOrderPayment.Id,
                    request.IdempotencyKey);
                request.ExistingTransactionReturned = true;
                return completedOrderPayment;
            }

            // Select provider based on currency and routing logic
            var provider = await _routingService.SelectProviderAsync(
                request.Currency,
                request.PreferredProvider,
                cancellationToken);

            _logger.LogInformation("Selected provider {ProviderName} for payment {IdempotencyKey}",
                provider.Name, request.IdempotencyKey);

            // Create payment transaction entity
            var transaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = request.IdempotencyKey,
                Amount = request.Amount,
                Currency = request.Currency,
                Status = PaymentStatus.Pending,
                CustomerId = request.CustomerId,
                OrderId = request.OrderId,
                Description = request.Description,
                PaymentProviderId = provider.Id,
                ProviderName = provider.Name,
                ProviderTransactionId = string.Empty, // Will be set after provider call
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                Metadata = request.Metadata,
                RetryCount = 0,
                CorrelationId = request.CorrelationId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

            };

            var providerMetadata = new Dictionary<string, string>(
                request.Metadata ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
            {
                ["transactionId"] = transaction.Id.ToString()
            };
            transaction.Metadata = providerMetadata;

            // Save initial transaction
            await _paymentRepository.AddAsync(transaction, cancellationToken);

            // Add initial transaction log
            await _paymentRepository.AddLogAsync(new TransactionLog
            {
                Id = Guid.NewGuid(),
                PaymentTransactionId = transaction.Id,
                PreviousStatus = null,
                NewStatus = PaymentStatus.Pending,
                EventType = "PaymentCreated",
                Message = $"Payment created for {request.Currency} {request.Amount}",
                CorrelationId = request.CorrelationId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            // Publish PaymentCreated event
            await _eventPublisher.PublishAsync(new PaymentCreatedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: nameof(PaymentCreatedEvent),
                MessageType: MessageType.Event,
                MessageVersion: "1.0.0",
                PublishedBy: "PaymentService",
                ConsumedBy: Array.Empty<string>(),
                CorrelationId: Guid.TryParse(request.CorrelationId, out var correlId) ? correlId : Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IsPublic: true,
                Payload: new PaymentCreatedEventPayload(
                    TransactionId: transaction.Id,
                    IdempotencyKey: transaction.IdempotencyKey,
                    Amount: (double)transaction.Amount,
                    Currency: transaction.Currency,
                    CustomerId: transaction.CustomerId,
                    OrderId: transaction.OrderId,
                    ProviderName: provider.Name
                )
            ), cancellationToken);

            // Process payment through provider with resilience
            var activeProvider = provider;
            ProviderPaymentResult? providerResult = null;
            var firstProviderFailureLogged = false;
            try
            {
                try
                {
                    providerResult = await ProcessPaymentWithProviderAsync(
                        request,
                        transaction,
                        activeProvider,
                        providerMetadata,
                        cancellationToken);
                }
                catch (Exception firstProviderException)
                {
                    _logger.LogWarning(
                        firstProviderException,
                        "Payment {TransactionId} threw while processing via {ProviderName}. Attempting fallback provider.",
                        transaction.Id,
                        activeProvider.Name);

                    _circuitBreakerStateManager.RecordStateChange(activeProvider.Name, true, DateTime.UtcNow);
                    transaction.Status = PaymentStatus.Failed;
                    transaction.ErrorMessage = firstProviderException.Message;
                    transaction.ProviderErrorCode = "PROVIDER_EXCEPTION";
                    transaction.UpdatedAt = DateTime.UtcNow;
                    await _paymentRepository.AddLogAsync(new TransactionLog
                    {
                        Id = Guid.NewGuid(),
                        PaymentTransactionId = transaction.Id,
                        PreviousStatus = PaymentStatus.Pending,
                        NewStatus = PaymentStatus.Failed,
                        EventType = "ProviderException",
                        Message = $"Provider {activeProvider.Name} threw while processing payment",
                        ErrorDetails = firstProviderException.ToString(),
                        CorrelationId = request.CorrelationId,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                    firstProviderFailureLogged = true;

                    var fallbackProvider = await _routingService.SelectProviderAsync(
                        request.Currency,
                        request.PreferredProvider,
                        cancellationToken);

                    if (fallbackProvider.Id == activeProvider.Id)
                    {
                        throw;
                    }

                    await _paymentRepository.AddLogAsync(new TransactionLog
                    {
                        Id = Guid.NewGuid(),
                        PaymentTransactionId = transaction.Id,
                        PreviousStatus = PaymentStatus.Failed,
                        NewStatus = PaymentStatus.Pending,
                        EventType = "ProviderFallback",
                        Message = $"Falling back from {activeProvider.Name} to {fallbackProvider.Name}",
                        CorrelationId = request.CorrelationId,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);

                    activeProvider = fallbackProvider;
                    providerResult = await ProcessPaymentWithProviderAsync(
                        request,
                        transaction,
                        activeProvider,
                        providerMetadata,
                        cancellationToken);
                }

                if (!providerResult.Success)
                {
                    _logger.LogWarning(
                        "Payment {TransactionId} failed via {ProviderName}. Error: {ErrorMessage}",
                        transaction.Id, activeProvider.Name, providerResult.ErrorMessage);

                    _circuitBreakerStateManager.RecordStateChange(activeProvider.Name, true, DateTime.UtcNow);
                    await AddProviderResponseLogAsync(
                        transaction,
                        PaymentStatus.Pending,
                        activeProvider,
                        providerResult,
                        request.CorrelationId,
                        cancellationToken);
                    firstProviderFailureLogged = true;

                    var fallbackProvider = await _routingService.SelectProviderAsync(
                        request.Currency,
                        request.PreferredProvider,
                        cancellationToken);

                    if (fallbackProvider.Id != activeProvider.Id)
                    {
                        await _paymentRepository.AddLogAsync(new TransactionLog
                        {
                            Id = Guid.NewGuid(),
                            PaymentTransactionId = transaction.Id,
                            PreviousStatus = PaymentStatus.Failed,
                            NewStatus = PaymentStatus.Pending,
                            EventType = "ProviderFallback",
                            Message = $"Falling back from {activeProvider.Name} to {fallbackProvider.Name}",
                            CorrelationId = request.CorrelationId,
                            CreatedAt = DateTime.UtcNow
                        }, cancellationToken);

                        activeProvider = fallbackProvider;
                        providerResult = await ProcessPaymentWithProviderAsync(
                            request,
                            transaction,
                            activeProvider,
                            providerMetadata,
                            cancellationToken);
                    }
                }

                if (providerResult.Success)
                {
                    _logger.LogInformation(
                        "Payment {TransactionId} processed successfully via {ProviderName}. Provider transaction: {ProviderTransactionId}",
                        transaction.Id, activeProvider.Name, providerResult.ProviderTransactionId);

                    await PublishPaymentPendingAsync(transaction, activeProvider, cancellationToken);
                }
                else
                {
                    _logger.LogError(
                        "Payment {TransactionId} failed via {ProviderName}. Error: {ErrorMessage}",
                        transaction.Id, activeProvider.Name, providerResult.ErrorMessage);

                    if (!firstProviderFailureLogged)
                    {
                        _circuitBreakerStateManager.RecordStateChange(activeProvider.Name, true, DateTime.UtcNow);
                    }

                    await PublishPaymentFailedAsync(
                        transaction,
                        activeProvider,
                        providerResult.ErrorMessage ?? "Unknown error",
                        providerResult.ErrorCode ?? "UNKNOWN",
                        cancellationToken);
                }

                await _paymentRepository.UpdateAsync(transaction, cancellationToken);

                // Add transaction log for provider response
                if (providerResult.Success || !firstProviderFailureLogged || activeProvider.Id != provider.Id)
                {
                    await AddProviderResponseLogAsync(
                        transaction,
                        activeProvider.Id == provider.Id ? PaymentStatus.Pending : PaymentStatus.Failed,
                        activeProvider,
                        providerResult,
                        request.CorrelationId,
                        cancellationToken);
                }

                // Record metrics
                stopwatch.Stop();
                _metricsService.RecordPaymentTransaction(
                    activeProvider.Name,
                    transaction.Status.ToString(),
                    transaction.Amount,
                    transaction.Currency);
                _metricsService.RecordPaymentDuration(
                    activeProvider.Name,
                    stopwatch.Elapsed.TotalSeconds);

                // Store result in idempotency cache
                await _idempotencyService.StoreResultAsync(
                    "payment",
                    request.IdempotencyKey,
                    transaction.Id.ToString(),
                    TimeSpan.FromHours(24),
                    cancellationToken);

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment {TransactionId} via provider {ProviderName}",
                    transaction.Id, activeProvider.Name);

                // Update transaction as failed
                transaction.Status = PaymentStatus.Failed;
                transaction.ErrorMessage = ex.Message;
                transaction.UpdatedAt = DateTime.UtcNow;

                await _paymentRepository.UpdateAsync(transaction, cancellationToken);

                // Add error log
                await _paymentRepository.AddLogAsync(new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PaymentTransactionId = transaction.Id,
                    PreviousStatus = PaymentStatus.Pending,
                    NewStatus = PaymentStatus.Failed,
                    EventType = "ProcessingError",
                    Message = $"Error processing payment: {ex.Message}",
                    ErrorDetails = ex.ToString(),
                    CorrelationId = request.CorrelationId,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                // Publish PaymentFailed event
                await _eventPublisher.PublishAsync(new PaymentFailedEvent(
                    MessageId: Guid.NewGuid(),
                    MessageName: nameof(PaymentFailedEvent),
                    MessageType: MessageType.Event,
                    MessageVersion: "1.0.0",
                    PublishedBy: "PaymentService",
                    ConsumedBy: PaymentFailedConsumers,
                    CorrelationId: Guid.TryParse(transaction.CorrelationId, out var correlIdFail2) ? correlIdFail2 : Guid.NewGuid(),
                    CausationId: null,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    IsPublic: true,
                    Payload: new PaymentFailedEventPayload(
                        TransactionId: transaction.Id,
                        IdempotencyKey: transaction.IdempotencyKey,
                        Amount: (double)transaction.Amount,
                        Currency: transaction.Currency,
                        CustomerId: transaction.CustomerId,
                        OrderId: ResolveOrderReference(transaction),
                        ProviderName: activeProvider.Name,
                        ErrorMessage: ex.Message,
                        ProviderErrorCode: string.Empty,
                        FailedAt: DateTimeOffset.UtcNow
                    )
                ), cancellationToken);

                // Record metrics for failed payment
                stopwatch.Stop();
                _metricsService.RecordPaymentTransaction(
                    activeProvider.Name,
                    PaymentStatus.Failed.ToString(),
                    transaction.Amount,
                    transaction.Currency);
                _metricsService.RecordPaymentDuration(
                    activeProvider.Name,
                    stopwatch.Elapsed.TotalSeconds);

                throw;
            }
        }
        finally
        {
            // Release distributed lock
            await _idempotencyService.ReleaseLockAsync("payment", request.IdempotencyKey, cancellationToken);
        }
    }

    private async Task<ProviderPaymentResult> ProcessPaymentWithProviderAsync(
        PaymentProcessingRequest request,
        PaymentTransaction transaction,
        PaymentProvider provider,
        Dictionary<string, string> providerMetadata,
        CancellationToken cancellationToken)
    {
        transaction.PaymentProviderId = provider.Id;
        transaction.ProviderName = provider.Name;
        transaction.ProviderTransactionId = string.Empty;
        transaction.PaymentUrl = null;
        transaction.Status = PaymentStatus.Pending;
        transaction.ErrorMessage = null;
        transaction.ProviderErrorCode = null;
        transaction.UpdatedAt = DateTime.UtcNow;

        var providerAdapter = _providerFactory.CreateProvider(provider);
        var providerRequest = new ProviderPaymentRequest
        {
            IdempotencyKey = request.IdempotencyKey,
            Amount = request.Amount,
            Currency = request.Currency,
            CustomerId = request.CustomerId,
            OrderId = request.OrderId,
            Description = request.Description,
            ReturnUrl = request.ReturnUrl,
            CancelUrl = request.CancelUrl,
            Metadata = providerMetadata
        };

        var providerResult = await providerAdapter.ProcessPaymentAsync(providerRequest, cancellationToken);

        transaction.ProviderTransactionId = providerResult.ProviderTransactionId;
        transaction.PaymentUrl = providerResult.PaymentUrl;
        transaction.Status = providerResult.Success ? PaymentStatus.Processing : PaymentStatus.Failed;
        transaction.ErrorMessage = providerResult.ErrorMessage;
        transaction.ProviderErrorCode = providerResult.ErrorCode;
        transaction.UpdatedAt = DateTime.UtcNow;

        return providerResult;
    }

    private Task PublishPaymentPendingAsync(
        PaymentTransaction transaction,
        PaymentProvider provider,
        CancellationToken cancellationToken)
    {
        var orderReference = ResolveOrderReference(transaction);

        return _eventPublisher.PublishAsync(new PaymentPendingEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentPendingEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "PaymentService",
            ConsumedBy: PaymentPendingConsumers,
            CorrelationId: Guid.TryParse(transaction.CorrelationId, out var correlIdPending) ? correlIdPending : Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentPendingEventPayload(
                TransactionId: transaction.Id,
                IdempotencyKey: transaction.IdempotencyKey,
                Amount: (double)transaction.Amount,
                Currency: transaction.Currency,
                CustomerId: transaction.CustomerId,
                OrderId: orderReference,
                ProviderName: provider.Name,
                ProviderEventCode: "ProviderSuccess",
                PendingAt: DateTimeOffset.UtcNow
            )
        ), cancellationToken);
    }

    private Task PublishPaymentFailedAsync(
        PaymentTransaction transaction,
        PaymentProvider provider,
        string errorMessage,
        string providerErrorCode,
        CancellationToken cancellationToken)
    {
        var orderReference = ResolveOrderReference(transaction);

        return _eventPublisher.PublishAsync(new PaymentFailedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(PaymentFailedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "PaymentService",
            ConsumedBy: PaymentFailedConsumers,
            CorrelationId: Guid.TryParse(transaction.CorrelationId, out var correlIdFail) ? correlIdFail : Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: true,
            Payload: new PaymentFailedEventPayload(
                TransactionId: transaction.Id,
                IdempotencyKey: transaction.IdempotencyKey,
                Amount: (double)transaction.Amount,
                Currency: transaction.Currency,
                CustomerId: transaction.CustomerId,
                OrderId: orderReference,
                ProviderName: provider.Name,
                ErrorMessage: errorMessage,
                ProviderErrorCode: providerErrorCode,
                FailedAt: DateTimeOffset.UtcNow
            )
        ), cancellationToken);
    }

    private static string ResolveOrderReference(PaymentTransaction transaction)
    {
        if (transaction.Metadata != null &&
            transaction.Metadata.TryGetValue("orderNumber", out var orderNumber) &&
            !string.IsNullOrWhiteSpace(orderNumber))
        {
            return orderNumber;
        }

        return transaction.OrderId;
    }

    private Task AddProviderResponseLogAsync(
        PaymentTransaction transaction,
        PaymentStatus previousStatus,
        PaymentProvider provider,
        ProviderPaymentResult providerResult,
        string correlationId,
        CancellationToken cancellationToken)
    {
        return _paymentRepository.AddLogAsync(new TransactionLog
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = transaction.Id,
            PreviousStatus = previousStatus,
            NewStatus = transaction.Status,
            EventType = providerResult.Success ? "ProviderSuccess" : "ProviderFailure",
            Message = providerResult.Success
                ? $"Provider {provider.Name} accepted payment"
                : $"Provider {provider.Name} rejected payment: {providerResult.ErrorMessage}",
            ProviderResponse = providerResult.RawResponse,
            ErrorDetails = providerResult.ErrorMessage,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<PaymentTransaction?> GetPaymentByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await _paymentRepository.GetByIdAsync(transactionId, cancellationToken);
    }

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPaymentsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _paymentRepository.GetPaymentsAsync(page, pageSize, cancellationToken);
    }
}
