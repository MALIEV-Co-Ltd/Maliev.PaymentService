using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
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
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        IPaymentRepository paymentRepository,
        IRefundRepository refundRepository,
        IProviderRepository providerRepository,
        IEventPublisher eventPublisher,
        IMetricsService metricsService,
        ILogger<RefundService> logger)
    {
        _paymentRepository = paymentRepository;
        _refundRepository = refundRepository;
        _providerRepository = providerRepository;
        _eventPublisher = eventPublisher;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<RefundTransaction> ProcessRefundAsync(
        Guid paymentTransactionId,
        decimal amount,
        string? reason,
        string refundType,
        CancellationToken cancellationToken = default)
    {
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
            IdempotencyKey = Guid.NewGuid().ToString(), // Will be overridden by controller with actual idempotency key
            CorrelationId = Guid.NewGuid(),
            InitiatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };

        // Save refund
        await _refundRepository.AddAsync(refund, cancellationToken);

        _logger.LogInformation(
            "Refund {RefundId} created for payment {PaymentId}. Amount: {Amount} {Currency}, Type: {Type}",
            refund.Id, paymentTransactionId, amount, payment.Currency, refundType);

        return refund;
    }
}
