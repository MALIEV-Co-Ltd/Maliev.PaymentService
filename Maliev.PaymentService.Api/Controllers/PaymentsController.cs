using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.PaymentService.Api.Authorization;
using Maliev.PaymentService.Application.Authorization;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Domain.Enums;
using Maliev.PaymentService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// Controller for payment processing operations.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("payment/v{version:apiVersion}/payments")]
[RequirePermission(PaymentPermissions.PaymentsRead)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IRefundService _refundService;
    private readonly IPaymentRoutingService _routingService;
    private readonly IMetricsService _metricsService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<PaymentsController> _logger;

    /// <summary>
    /// Initializes a new instance of the PaymentsController.
    /// </summary>
    /// <param name="paymentService">Payment processing service</param>
    /// <param name="refundService">Refund processing service</param>
    /// <param name="routingService">Payment provider routing service</param>
    /// <param name="metricsService">Metrics collection service</param>
    /// <param name="cache">Distributed cache for performance</param>
    /// <param name="logger">Logger instance</param>
    public PaymentsController(
        IPaymentService paymentService,
        IRefundService refundService,
        IPaymentRoutingService routingService,
        IMetricsService metricsService,
        IDistributedCache cache,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _refundService = refundService;
        _routingService = routingService;
        _metricsService = metricsService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Process a new payment.
    /// Requires Idempotency-Key header to prevent duplicate payments.
    /// </summary>
    /// <param name="request">Payment request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created payment transaction</returns>
    [HttpPost]
    [RequirePermission(PaymentPermissions.PaymentsProcess)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment(
        [FromBody] PaymentRequest request,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            // Validate Idempotency-Key header
            if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) ||
                string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new { error = "Idempotency-Key header is required" });
            }

            // Get Correlation-Id header (optional)
            Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId);
            var correlationIdValue = string.IsNullOrWhiteSpace(correlationId)
                ? Guid.NewGuid().ToString()
                : correlationId.ToString();

            // Validate currency is supported
            var supportedProvider = await _routingService.SelectProviderAsync(
                request.Currency,
                request.PreferredProvider,
                cancellationToken);

            if (supportedProvider == null)
            {
                return BadRequest(new { error = $"Currency {request.Currency} is not supported by any active provider" });
            }

            // Create payment processing request
            var processingRequest = new PaymentProcessingRequest
            {
                IdempotencyKey = idempotencyKey!,
                Amount = request.Amount,
                Currency = request.Currency,
                CustomerId = request.CustomerId,
                OrderId = request.OrderId,
                Description = request.Description,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                Metadata = request.Metadata,
                PreferredProvider = request.PreferredProvider,
                CorrelationId = correlationIdValue
            };

            // Process payment
            var transaction = await _paymentService.ProcessPaymentAsync(processingRequest, cancellationToken);

            // Map to response
            var response = MapToPaymentResponse(transaction);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _metricsService.RecordPaymentDuration(transaction.PaymentProvider?.Name ?? "unknown", duration);

            // Return 200 OK if idempotent request (existing transaction)
            if (transaction.CreatedAt < DateTime.UtcNow.AddSeconds(-1))
            {
                _logger.LogInformation(
                    "Returning existing payment {TransactionId} for idempotent request {IdempotencyKey}",
                    transaction.Id, idempotencyKey);
                return Ok(response);
            }

            return CreatedAtAction(
                nameof(GetPaymentById),
                new { id = transaction.Id },
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid payment request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment");
            return StatusCode(500, new { error = "An error occurred while processing the payment" });
        }
    }

    /// <summary>
    /// Get a payment by its transaction ID.
    /// Results are cached for performance.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Payment details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            // Try to get from cache first
            var cacheKey = $"payment:{id}";
            var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedValue))
            {
                if (JsonSerializer.Deserialize<PaymentResponse>(cachedValue) is { } cachedResponse)
                {
                    _logger.LogDebug("Payment {TransactionId} retrieved from cache", id);
                    _metricsService.RecordPaymentStatusCacheHit(cachedResponse.SelectedProvider ?? "unknown");
                    return Ok(cachedResponse);
                }
            }

            // Not in cache, get from database
            var transaction = await _paymentService.GetPaymentByIdAsync(id, cancellationToken);

            if (transaction == null)
            {
                return NotFound(new { error = $"Payment {id} not found" });
            }

            var response = MapToPaymentResponse(transaction);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _metricsService.RecordPaymentStatusQuery(transaction.PaymentProvider?.Name ?? "unknown", duration);

            // Cache the result
            var cacheDuration = transaction.Status switch
            {
                PaymentStatus.Pending => TimeSpan.FromSeconds(60), // Short cache for pending
                PaymentStatus.Processing => TimeSpan.FromSeconds(60),
                PaymentStatus.Completed => TimeSpan.FromMinutes(60), // Longer cache for completed
                PaymentStatus.Failed => TimeSpan.FromMinutes(30),
                PaymentStatus.Refunded => TimeSpan.FromMinutes(30),
                PaymentStatus.PartiallyRefunded => TimeSpan.FromMinutes(30),
                _ => TimeSpan.FromMinutes(5)
            };

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration
            };

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                cacheOptions,
                cancellationToken);

            _logger.LogDebug("Payment {TransactionId} cached for {Duration}", id, cacheDuration);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {TransactionId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the payment" });
        }
    }

    /// <summary>
    /// Process a refund for a completed payment.
    /// </summary>
    /// <param name="id">Payment transaction ID to refund</param>
    /// <param name="request">Refund request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Refund transaction details</returns>
    [HttpPost("{id:guid}/refund")]
    [RequirePermission(PaymentPermissions.PaymentsRefund)]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefundResponse>> ProcessRefund(
        Guid id,
        [FromBody] RefundRequest request,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            // Validate Idempotency-Key header
            if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) ||
                string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return BadRequest(new { error = "Idempotency-Key header is required" });
            }

            // Process refund
            var refundTransaction = await _refundService.ProcessRefundAsync(
                id,
                request.Amount,
                request.Reason,
                request.RefundType,
                cancellationToken);

            // Map to response
            var response = new RefundResponse
            {
                RefundId = refundTransaction.Id,
                PaymentTransactionId = refundTransaction.PaymentTransactionId,
                Amount = refundTransaction.Amount,
                Currency = refundTransaction.Currency,
                Status = refundTransaction.Status.ToString().ToLowerInvariant(),
                Reason = refundTransaction.Reason,
                RefundType = refundTransaction.RefundType,
                ProviderRefundId = refundTransaction.ProviderRefundId,
                ErrorMessage = refundTransaction.ErrorMessage,
                InitiatedAt = refundTransaction.InitiatedAt,
                CompletedAt = refundTransaction.CompletedAt,
                CreatedAt = refundTransaction.CreatedAt,
                UpdatedAt = refundTransaction.UpdatedAt
            };

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _metricsService.RecordRefundTransaction(
                refundTransaction.Provider?.Name ?? "unknown",
                refundTransaction.Status.ToString(),
                refundTransaction.Amount);

            // Invalidate payment cache since refund affects payment state
            var cacheKey = $"payment:{id}";
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid refund request for payment {PaymentId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for payment {PaymentId}", id);
            return StatusCode(500, new { error = "An error occurred while processing the refund" });
        }
    }

    /// <summary>
    /// Maps a payment transaction entity to API response model.
    /// </summary>
    private static PaymentResponse MapToPaymentResponse(Maliev.PaymentService.Domain.Entities.PaymentTransaction transaction)
    {
        return new PaymentResponse
        {
            TransactionId = transaction.Id,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            CustomerId = transaction.CustomerId,
            OrderId = transaction.OrderId,
            Description = transaction.Description ?? string.Empty,
            SelectedProvider = transaction.PaymentProvider?.Name ?? transaction.ProviderName ?? "Unknown",
            ProviderTransactionId = transaction.ProviderTransactionId ?? string.Empty,
            PaymentUrl = transaction.PaymentUrl,
            Metadata = transaction.Metadata,
            ErrorMessage = transaction.ErrorMessage,
            ProviderErrorCode = transaction.ProviderErrorCode,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            CompletedAt = transaction.CompletedAt
        };
    }
}
