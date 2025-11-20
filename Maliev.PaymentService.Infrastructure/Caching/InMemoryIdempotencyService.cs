using System.Collections.Concurrent;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.PaymentService.Infrastructure.Caching;

/// <summary>
/// In-memory idempotency service for development mode when Redis is disabled.
/// WARNING: Not suitable for production - data is not persisted and not shared across instances.
/// </summary>
public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, string> _results = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<InMemoryIdempotencyService> _logger;

    public InMemoryIdempotencyService(ILogger<InMemoryIdempotencyService> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "Using in-memory idempotency service - NOT SUITABLE FOR PRODUCTION. " +
            "Enable Redis for production deployments.");
    }

    public Task<bool> IsProcessedAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        var exists = _results.ContainsKey(key);
        _logger.LogDebug("Idempotency check for {Key}: {Exists}", key, exists);
        return Task.FromResult(exists);
    }

    public Task StoreResultAsync(string operationType, string idempotencyKey, string result, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        _results.TryAdd(key, result);
        _logger.LogDebug("Stored idempotency result for {Key} (TTL ignored in-memory)", key);

        // Note: In-memory implementation doesn't enforce TTL expiration
        // For production, use Redis which handles TTL properly
        return Task.CompletedTask;
    }

    public Task<string?> GetResultAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        _results.TryGetValue(key, out var result);
        _logger.LogDebug("Retrieved idempotency result for {Key}: {Found}", key, result != null);
        return Task.FromResult(result);
    }

    public Task<bool> AcquireLockAsync(string operationType, string idempotencyKey, TimeSpan lockTimeout, CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(operationType, idempotencyKey);
        var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        var acquired = semaphore.Wait(0, cancellationToken);
        _logger.LogDebug("Lock acquisition for {Key}: {Acquired}", lockKey, acquired);
        return Task.FromResult(acquired);
    }

    public Task ReleaseLockAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(operationType, idempotencyKey);

        if (_locks.TryGetValue(lockKey, out var semaphore))
        {
            try
            {
                semaphore.Release();
                _logger.LogDebug("Released lock for {Key}", lockKey);
            }
            catch (SemaphoreFullException)
            {
                _logger.LogWarning("Attempted to release lock that was not held: {Key}", lockKey);
            }
        }

        return Task.CompletedTask;
    }

    private static string GetKey(string operationType, string idempotencyKey)
        => $"idempotency:{operationType}:{idempotencyKey}";

    private static string GetLockKey(string operationType, string idempotencyKey)
        => $"lock:{operationType}:{idempotencyKey}";
}
