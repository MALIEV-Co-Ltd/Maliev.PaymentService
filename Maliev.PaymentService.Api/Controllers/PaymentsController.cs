using FluentValidation;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// API controller for payment processing.
/// Handles payment initiation and status queries.
/// </summary>
[ApiController]
[Route("payments/v1/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentStatusService _paymentStatusService;
    private readonly IRefundService _refundService;
    private readonly IMetricsService _metricsService;
    private readonly IValidator<PaymentRequest> _validator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService,
        IPaymentStatusService paymentStatusService,
        IRefundService refundService,
        IMetricsService metricsService,
        IValidator<PaymentRequest> validator,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _paymentStatusService = paymentStatusService;
        _refundService = refundService;
        _metricsService = metricsService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Process a payment through the gateway.
    /// </summary>
    /// <param name="request">The payment request containing amount, currency, customer details, and optional provider preference.</param>
    /// <returns>A payment response with transaction details and payment URL for completion.</returns>
    /// <remarks>
    /// This endpoint initiates a payment transaction through the payment gateway. It supports idempotency through the Idempotency-Key header,
    /// ensuring duplicate requests with the same key return the same result.
    ///
    /// **Required Headers:**
    /// - `Authorization`: Bearer token for authentication
    /// - `Idempotency-Key`: Unique identifier for this payment request (UUID recommended)
    /// - `X-Correlation-Id` (optional): For distributed tracing across services
    ///
    /// **Payment Flow:**
    /// 1. Request is validated for required fields and business rules
    /// 2. Provider is automatically selected based on currency, health, and priority
    /// 3. Payment is initiated with the selected provider
    /// 4. Transaction is persisted to database
    /// 5. Events are published for downstream processing
    ///
    /// **Status Codes:**
    /// - `201 Created`: Payment successfully created (new transaction)
    /// - `200 OK`: Existing payment returned (idempotent request)
    /// - `400 Bad Request`: Invalid request data or missing Idempotency-Key
    /// - `500 Internal Server Error`: Unexpected error during payment processing
    ///
    /// **Example Request:**
    /// ```json
    /// {
    ///   "amount": 99.99,
    ///   "currency": "USD",
    ///   "customerId": "cust_12345",
    ///   "orderId": "order_67890",
    ///   "description": "Premium subscription",
    ///   "returnUrl": "https://example.com/payment/success",
    ///   "cancelUrl": "https://example.com/payment/cancel",
    ///   "preferredProvider": "stripe",
    ///   "metadata": {
    ///     "plan": "premium",
    ///     "period": "annual"
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <response code="201">Payment successfully created. Returns transaction details and payment URL.</response>
    /// <response code="200">Idempotent request. Returns existing transaction details.</response>
    /// <response code="400">Invalid request. Missing required fields or invalid Idempotency-Key.</response>
    /// <response code="500">Internal server error. Payment processing failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] PaymentRequest request)
    {
        // Extract Idempotency-Key header (required)
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _logger.LogWarning("Payment request received without Idempotency-Key header");
            return BadRequest(new ErrorResponse
            {
                Error = "IDEMPOTENCY_KEY_REQUIRED",
                Message = "Idempotency-Key header is required",
                Timestamp = DateTime.UtcNow
            });
        }

        // Extract X-Correlation-Id header (optional, generate if not provided)
        if (!Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store correlation ID in HttpContext for middleware logging
        HttpContext.Items["CorrelationId"] = correlationId.ToString();

        // Validate request
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            _logger.LogWarning("Payment request validation failed. Errors: {Errors}", errors);

            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "One or more validation errors occurred",
                Details = errors.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value),
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            });
        }

        _logger.LogInformation(
            "Processing payment request. IdempotencyKey: {IdempotencyKey}, Amount: {Amount}, Currency: {Currency}, OrderId: {OrderId}, CorrelationId: {CorrelationId}",
            idempotencyKey, request.Amount, request.Currency, request.OrderId, correlationId);

        try
        {
            var processingRequest = new PaymentProcessingRequest
            {
                IdempotencyKey = idempotencyKey!,
                Amount = request.Amount,
                Currency = request.Currency.ToUpperInvariant(),
                CustomerId = request.CustomerId,
                OrderId = request.OrderId,
                Description = request.Description,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                Metadata = request.Metadata,
                PreferredProvider = request.PreferredProvider,
                CorrelationId = correlationId!
            };

            var transaction = await _paymentService.ProcessPaymentAsync(processingRequest);

            var response = MapToResponse(transaction);

            // Return 200 OK if idempotent request (existing transaction)
            // Return 201 Created if new transaction
            if (transaction.CreatedAt < DateTime.UtcNow.AddSeconds(-1))
            {
                _logger.LogInformation(
                    "Returning existing payment {TransactionId} for idempotent request {IdempotencyKey}",
                    transaction.Id, idempotencyKey);
                return Ok(response);
            }

            _logger.LogInformation(
                "Payment {TransactionId} created successfully. Status: {Status}, Provider: {ProviderName}",
                transaction.Id, transaction.Status, transaction.ProviderName);

            return CreatedAtAction(
                nameof(GetPaymentById),
                new { id = transaction.Id },
                response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Payment processing failed due to business logic error");
            return BadRequest(new ErrorResponse
            {
                Error = "PAYMENT_PROCESSING_ERROR",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing payment");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "An error occurred while processing the payment",
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            });
        }
    }

    /// <summary>
    /// Get payment details by transaction ID.
    /// </summary>
    /// <param name="id">The unique transaction ID (UUID) of the payment.</param>
    /// <returns>The current payment details including status, amount, and provider information.</returns>
    /// <remarks>
    /// Retrieves payment transaction details with intelligent caching to improve performance.
    ///
    /// **Caching Strategy:**
    /// - Active payments (pending, processing): 60 seconds TTL
    /// - Terminal states (completed, failed, cancelled): 3600 seconds TTL
    /// - Cache-aside pattern with Redis backing store
    ///
    /// **Required Headers:**
    /// - `Authorization`: Bearer token for authentication
    ///
    /// **Status Values:**
    /// - `pending`: Payment awaiting customer action
    /// - `processing`: Payment being processed by provider
    /// - `completed`: Payment successfully completed
    /// - `failed`: Payment failed (check errorMessage for details)
    /// - `cancelled`: Payment cancelled by customer or system
    ///
    /// **Performance:**
    /// - Cache hit: ~5-10ms response time
    /// - Cache miss: ~50-100ms response time
    ///
    /// **Example Response:**
    /// ```json
    /// {
    ///   "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "amount": 99.99,
    ///   "currency": "USD",
    ///   "status": "completed",
    ///   "customerId": "cust_12345",
    ///   "orderId": "order_67890",
    ///   "selectedProvider": "stripe",
    ///   "providerTransactionId": "pi_3L8K9M2eZvKYlo2C0w8TZ9Gh",
    ///   "completedAt": "2025-11-19T10:35:00Z",
    ///   "createdAt": "2025-11-19T10:30:00Z",
    ///   "updatedAt": "2025-11-19T10:35:00Z"
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Payment found. Returns current payment details.</response>
    /// <response code="404">Payment not found with the specified transaction ID.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentById(Guid id)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Use caching service (with automatic cache hit/miss logging)
            var transaction = await _paymentStatusService.GetPaymentStatusAsync(id);

            if (transaction == null)
            {
                _logger.LogInformation("Payment {TransactionId} not found", id);
                return NotFound(new ErrorResponse
                {
                    Error = "PAYMENT_NOT_FOUND",
                    Message = $"Payment with ID {id} not found",
                    Timestamp = DateTime.UtcNow
                });
            }

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            // Record metrics
            _metricsService.RecordPaymentStatusQuery(transaction.ProviderName ?? "unknown", duration);

            _logger.LogInformation("Retrieved payment {TransactionId}, Status: {Status}", id, transaction.Status);

            return Ok(MapToResponse(transaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {TransactionId}", id);

            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "An error occurred while retrieving the payment",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Process a refund for a completed payment.
    /// </summary>
    /// <param name="transactionId">The unique transaction ID (UUID) of the payment to refund.</param>
    /// <param name="request">The refund request containing amount, reason, and refund type.</param>
    /// <returns>A refund response with refund details and status.</returns>
    /// <remarks>
    /// Initiates a refund for a previously completed payment. Supports both full and partial refunds
    /// with validation to prevent over-refunding.
    ///
    /// **Required Headers:**
    /// - `Authorization`: Bearer token for authentication
    /// - `Idempotency-Key`: Unique identifier for this refund request (UUID recommended)
    /// - `X-Correlation-Id` (optional): For distributed tracing across services
    ///
    /// **Refund Types:**
    /// - `full`: Refund the entire payment amount
    /// - `partial`: Refund a specific amount (must be ≤ remaining refundable amount)
    ///
    /// **Validation Rules:**
    /// 1. Payment must be in `completed` status
    /// 2. Refund amount must be greater than zero
    /// 3. Refund amount cannot exceed remaining refundable amount
    /// 4. Multiple partial refunds are supported (sum ≤ original payment amount)
    ///
    /// **Refund Status:**
    /// - `pending`: Refund initiated, awaiting provider confirmation
    /// - `processing`: Refund being processed by provider
    /// - `completed`: Refund successfully completed
    /// - `failed`: Refund failed (check errorMessage for details)
    ///
    /// **Processing Time:**
    /// - Immediate: Provider API call (~1-2 seconds)
    /// - Settlement: 5-10 business days to customer account (provider-dependent)
    ///
    /// **Example Request:**
    /// ```json
    /// {
    ///   "amount": 49.99,
    ///   "reason": "Customer requested partial refund",
    ///   "refundType": "partial"
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Refund successfully processed. Returns refund details and status.</response>
    /// <response code="400">Invalid request. Payment not completed or refund amount invalid.</response>
    /// <response code="404">Payment not found with the specified transaction ID.</response>
    /// <response code="500">Internal server error. Refund processing failed.</response>
    [HttpPost("{transactionId}/refund")]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RefundResponse>> ProcessRefund(
        Guid transactionId,
        [FromBody] RefundRequest request)
    {
        var startTime = DateTime.UtcNow;

        // Extract headers
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _logger.LogWarning("Refund request received without Idempotency-Key header");
            return BadRequest(new ErrorResponse
            {
                Error = "MISSING_IDEMPOTENCY_KEY",
                Message = "Idempotency-Key header is required for refund requests",
                Timestamp = DateTime.UtcNow
            });
        }

        var correlationId = Request.Headers.TryGetValue("X-Correlation-Id", out var corrId)
            ? corrId.ToString()
            : Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Processing refund request for payment {TransactionId}. Amount: {Amount}, Type: {Type}, IdempotencyKey: {IdempotencyKey}, CorrelationId: {CorrelationId}",
            transactionId, request.Amount, request.RefundType, idempotencyKey, correlationId);

        try
        {
            // Process refund
            var refund = await _refundService.ProcessRefundAsync(
                transactionId,
                request.Amount,
                request.Reason,
                request.RefundType);

            var response = MapToRefundResponse(refund);

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _metricsService.RecordRefundTransaction(
                refund.Provider?.Name ?? "unknown",
                refund.Status.ToString(),
                refund.Amount);

            _logger.LogInformation(
                "Refund {RefundId} processed successfully for payment {TransactionId}. Status: {Status}, Amount: {Amount}",
                refund.Id, transactionId, refund.Status, refund.Amount);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid refund request for payment {TransactionId}", transactionId);

            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_REFUND",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for payment {TransactionId}", transactionId);

            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "An error occurred while processing the refund",
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            });
        }
    }

    private static PaymentResponse MapToResponse(Core.Entities.PaymentTransaction transaction)
    {
        return new PaymentResponse
        {
            TransactionId = transaction.Id,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            CustomerId = transaction.CustomerId,
            OrderId = transaction.OrderId,
            Description = transaction.Description,
            SelectedProvider = transaction.ProviderName,
            ProviderTransactionId = transaction.ProviderTransactionId,
            PaymentUrl = transaction.PaymentUrl,
            Metadata = transaction.Metadata,
            ErrorMessage = transaction.ErrorMessage,
            ProviderErrorCode = transaction.ProviderErrorCode,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
            CompletedAt = transaction.CompletedAt
        };
    }

    private static RefundResponse MapToRefundResponse(Core.Entities.RefundTransaction refund)
    {
        return new RefundResponse
        {
            RefundId = refund.Id,
            PaymentTransactionId = refund.PaymentTransactionId,
            Amount = refund.Amount,
            Currency = refund.Currency,
            Status = refund.Status.ToString(),
            Reason = refund.Reason,
            RefundType = refund.RefundType,
            ProviderRefundId = refund.ProviderRefundId,
            ErrorMessage = refund.ErrorMessage,
            InitiatedAt = refund.InitiatedAt,
            CompletedAt = refund.CompletedAt,
            CreatedAt = refund.CreatedAt,
            UpdatedAt = refund.UpdatedAt
        };
    }
}
