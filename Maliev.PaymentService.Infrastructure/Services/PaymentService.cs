using System.Diagnostics;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Providers;
using Maliev.PaymentService.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Payment orchestration service.
/// Coordinates idempotency, routing, provider calls, resilience, and event publishing.
/// </summary>
public class PaymentService : IPaymentService
{
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
            return existingTransaction;
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
                return existingTransaction;
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
            await _eventPublisher.PublishAsync(new Core.Events.PaymentCreatedEvent
            {
                TransactionId = transaction.Id,
                IdempotencyKey = transaction.IdempotencyKey,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                CustomerId = transaction.CustomerId,
                OrderId = transaction.OrderId,
                ProviderName = provider.Name,
                CreatedAt = transaction.CreatedAt,
                CorrelationId = transaction.CorrelationId
            }, cancellationToken);

            // Process payment through provider with resilience
            ProviderPaymentResult? providerResult = null;
            try
            {
                var providerAdapter = _providerFactory.CreateProvider(provider);

                var providerRequest = new ProviderPaymentRequest
                {
                    Amount = request.Amount,
                    Currency = request.Currency,
                    CustomerId = request.CustomerId,
                    OrderId = request.OrderId,
                    Description = request.Description,
                    ReturnUrl = request.ReturnUrl,
                    CancelUrl = request.CancelUrl,
                    Metadata = request.Metadata
                };

                // Call provider (Polly resilience is applied at HTTP client level in production)
                providerResult = await providerAdapter.ProcessPaymentAsync(providerRequest, cancellationToken);

                // Update transaction with provider response
                transaction.ProviderTransactionId = providerResult.ProviderTransactionId;
                transaction.PaymentUrl = providerResult.PaymentUrl;
                transaction.Status = providerResult.Success ? PaymentStatus.Processing : PaymentStatus.Failed;
                transaction.ErrorMessage = providerResult.ErrorMessage;
                transaction.ProviderErrorCode = providerResult.ErrorCode;
                transaction.UpdatedAt = DateTime.UtcNow;

                if (providerResult.Success)
                {
                    _logger.LogInformation(
                        "Payment {TransactionId} processed successfully via {ProviderName}. Provider transaction: {ProviderTransactionId}",
                        transaction.Id, provider.Name, providerResult.ProviderTransactionId);
                }
                else
                {
                    _logger.LogError(
                        "Payment {TransactionId} failed via {ProviderName}. Error: {ErrorMessage}",
                        transaction.Id, provider.Name, providerResult.ErrorMessage);

                    // Record circuit breaker state change if failure
                    _circuitBreakerStateManager.RecordStateChange(provider.Name, false, DateTime.UtcNow);

                    // Publish PaymentFailed event
                    await _eventPublisher.PublishAsync(new Core.Events.PaymentFailedEvent
                    {
                        TransactionId = transaction.Id,
                        IdempotencyKey = transaction.IdempotencyKey,
                        Amount = transaction.Amount,
                        Currency = transaction.Currency,
                        CustomerId = transaction.CustomerId,
                        OrderId = transaction.OrderId,
                        ProviderName = provider.Name,
                        ErrorMessage = providerResult.ErrorMessage ?? "Unknown error",
                        ProviderErrorCode = providerResult.ErrorCode,
                        FailedAt = DateTime.UtcNow,
                        CorrelationId = transaction.CorrelationId
                    }, cancellationToken);
                }

                await _paymentRepository.UpdateAsync(transaction, cancellationToken);

                // Add transaction log for provider response
                await _paymentRepository.AddLogAsync(new TransactionLog
                {
                    Id = Guid.NewGuid(),
                    PaymentTransactionId = transaction.Id,
                    PreviousStatus = PaymentStatus.Pending,
                    NewStatus = transaction.Status,
                    EventType = providerResult.Success ? "ProviderSuccess" : "ProviderFailure",
                    Message = providerResult.Success
                        ? $"Provider {provider.Name} accepted payment"
                        : $"Provider {provider.Name} rejected payment: {providerResult.ErrorMessage}",
                    ProviderResponse = providerResult.RawResponse,
                    ErrorDetails = providerResult.ErrorMessage,
                    CorrelationId = request.CorrelationId,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                // Record metrics
                stopwatch.Stop();
                _metricsService.RecordPaymentTransaction(
                    provider.Name,
                    transaction.Status.ToString(),
                    transaction.Amount,
                    transaction.Currency);
                _metricsService.RecordPaymentDuration(
                    provider.Name,
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
                    transaction.Id, provider.Name);

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
                await _eventPublisher.PublishAsync(new Core.Events.PaymentFailedEvent
                {
                    TransactionId = transaction.Id,
                    IdempotencyKey = transaction.IdempotencyKey,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    CustomerId = transaction.CustomerId,
                    OrderId = transaction.OrderId,
                    ProviderName = provider.Name,
                    ErrorMessage = ex.Message,
                    ProviderErrorCode = null,
                    FailedAt = DateTime.UtcNow,
                    CorrelationId = transaction.CorrelationId
                }, cancellationToken);

                // Record metrics for failed payment
                stopwatch.Stop();
                _metricsService.RecordPaymentTransaction(
                    provider.Name,
                    PaymentStatus.Failed.ToString(),
                    transaction.Amount,
                    transaction.Currency);
                _metricsService.RecordPaymentDuration(
                    provider.Name,
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

    public async Task<PaymentTransaction?> GetPaymentByIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await _paymentRepository.GetByIdAsync(transactionId, cancellationToken);
    }
}
