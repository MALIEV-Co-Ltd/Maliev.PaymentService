using Maliev.PaymentService.Domain.Entities;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Providers;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Service for refund processing with validation and provider integration.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IRefundRepository _refundRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMetricsService _metricsService;
    private readonly ProviderFactory _providerFactory;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IPaymentRepository paymentRepository,
        IRefundRepository refundRepository,
        IProviderRepository providerRepository,
        IEventPublisher eventPublisher,
        IMetricsService metricsService,
        ProviderFactory providerFactory,
        ILogger<RefundService> logger)
    {
        _paymentRepository = paymentRepository;
        _refundRepository = refundRepository;
        _providerRepository = providerRepository;
        _eventPublisher = eventPublisher;
        _metricsService = metricsService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<RefundTransaction> ProcessRefundAsync(
        Guid paymentTransactionId,
        decimal amount,
        string? reason,
        string refundType,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Refund idempotency key is required", nameof(idempotencyKey));
        }

        var existingRefund = await _refundRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingRefund != null)
        {
            _logger.LogInformation(
                "Idempotent refund request detected. Returning existing refund {RefundId}",
                existingRefund.Id);
            return existingRefund;
        }

        // Validate amount
        if (amount <= 0)
        {
            throw new ArgumentException("Refund amount must be greater than zero", nameof(amount));
        }

        // Get payment transaction
        var payment = await _paymentRepository.GetByIdAsync(paymentTransactionId, cancellationToken);
        if (payment == null)
        {
            throw new InvalidOperationException($"Payment transaction {paymentTransactionId} not found");
        }

        // Validate payment status
        if (payment.Status != PaymentStatus.Completed)
        {
            throw new InvalidOperationException($"Payment {paymentTransactionId} is not completed. Current status: {payment.Status}");
        }

        // Get existing refunds
        var existingRefunds = await _refundRepository.GetByPaymentTransactionIdAsync(paymentTransactionId, cancellationToken);

        // Calculate total refunded amount (only completed refunds)
        var totalRefunded = existingRefunds
            .Where(r => r.Status == RefundStatus.Completed)
            .Sum(r => r.Amount);

        var remainingRefundable = payment.Amount - totalRefunded;

        // Validate refund amount doesn't exceed remaining
        if (amount > remainingRefundable)
        {
            throw new InvalidOperationException(
                $"Refund amount {amount} exceeds remaining refundable amount {remainingRefundable}. " +
                $"Payment amount: {payment.Amount}, Total refunded: {totalRefunded}");
        }

        var provider = await _providerRepository.GetByIdAsync(payment.PaymentProviderId, cancellationToken);
        if (provider == null)
        {
            throw new InvalidOperationException($"Payment provider {payment.PaymentProviderId} not found for refund");
        }

        // Create refund transaction
        var refund = new RefundTransaction
        {
            Id = Guid.NewGuid(),
            PaymentTransactionId = paymentTransactionId,
            ProviderId = payment.PaymentProviderId,
            Amount = amount,
            Currency = payment.Currency,
            Status = RefundStatus.Pending,
            Reason = reason,
            RefundType = refundType,
            IdempotencyKey = idempotencyKey,
            CorrelationId = Guid.NewGuid(),
            InitiatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save refund
        await _refundRepository.AddAsync(refund, cancellationToken);

        var adapter = _providerFactory.CreateProvider(provider);
        var providerResult = await adapter.ProcessRefundAsync(new ProviderRefundRequest
        {
            IdempotencyKey = idempotencyKey,
            ProviderTransactionId = payment.ProviderTransactionId,
            Amount = amount,
            Currency = payment.Currency,
            Reason = reason ?? refundType,
            Metadata = new Dictionary<string, string>
            {
                ["refundId"] = refund.Id.ToString(),
                ["paymentTransactionId"] = payment.Id.ToString(),
                ["orderId"] = payment.OrderId,
                ["refundType"] = refundType
            }
        }, cancellationToken);

        refund.ProviderRefundId = providerResult.ProviderRefundId;
        refund.UpdatedAt = DateTime.UtcNow;

        if (providerResult.Success)
        {
            refund.Status = RefundStatus.Completed;
            refund.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            refund.Status = RefundStatus.Failed;
            refund.ErrorMessage = providerResult.ErrorMessage;
            refund.ProviderErrorCode = providerResult.ErrorCode;
            refund.FailedAt = DateTime.UtcNow;
        }

        await _refundRepository.UpdateAsync(refund, cancellationToken);

        _logger.LogInformation(
            "Refund {RefundId} processed for payment {PaymentId}. Amount: {Amount} {Currency}, Type: {Type}, Status: {Status}",
            refund.Id, paymentTransactionId, amount, payment.Currency, refundType, refund.Status);

        return refund;
    }
}
