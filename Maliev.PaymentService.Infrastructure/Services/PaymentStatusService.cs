using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Service for payment status queries with Redis caching.
/// Active transactions cached for 60 seconds, terminal states for 1 hour.
/// </summary>
public class PaymentStatusService : IPaymentStatusService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IDistributedCache _cache;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<PaymentStatusService> _logger;

    private const int ActiveTransactionCacheTtlSeconds = 60;
    private const int TerminalTransactionCacheTtlSeconds = 3600;

    public PaymentStatusService(
        IPaymentRepository paymentRepository,
        IDistributedCache cache,
        IMetricsService metricsService,
        ILogger<PaymentStatusService> logger)
    {
        _paymentRepository = paymentRepository;
        _cache = cache;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<PaymentTransaction?> GetPaymentStatusAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"payment_status:{transactionId}";

        try
        {
            // Try cache first
            var cachedData = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                var cached = JsonSerializer.Deserialize<PaymentTransaction>(cachedData);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for payment status: {TransactionId}", transactionId);
                    _metricsService.RecordPaymentStatusCacheHit(cached.ProviderName ?? "unknown");
                    return cached;
                }
            }

            _logger.LogDebug("Cache miss for payment status: {TransactionId}", transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading from cache for payment {TransactionId}, falling back to database", transactionId);
        }

        // Query database
        var transaction = await _paymentRepository.GetByIdAsync(transactionId, cancellationToken);

        if (transaction == null)
        {
            _logger.LogDebug("Payment not found: {TransactionId}", transactionId);
            return null;
        }

        // Cache result with appropriate TTL
        try
        {
            var ttl = GetCacheTtl(transaction.Status);
            var serialized = JsonSerializer.SerializeToUtf8Bytes(transaction);

            await _cache.SetAsync(
                cacheKey,
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
                },
                cancellationToken);

            _logger.LogDebug(
                "Cached payment status for {TransactionId} with TTL {Ttl}s (Status: {Status})",
                transactionId, ttl, transaction.Status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing to cache for payment {TransactionId}", transactionId);
            // Continue even if caching fails
        }

        return transaction;
    }

    private int GetCacheTtl(PaymentStatus status)
    {
        // Terminal states (completed/failed) cache for 1 hour
        // Active states (pending/processing) cache for 60 seconds
        return status switch
        {
            PaymentStatus.Completed => TerminalTransactionCacheTtlSeconds,
            PaymentStatus.Failed => TerminalTransactionCacheTtlSeconds,
            PaymentStatus.Pending => ActiveTransactionCacheTtlSeconds,
            PaymentStatus.Processing => ActiveTransactionCacheTtlSeconds,
            _ => ActiveTransactionCacheTtlSeconds
        };
    }
}
