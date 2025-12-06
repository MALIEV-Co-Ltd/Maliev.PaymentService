using System.Collections.Concurrent;
using Maliev.PaymentService.Core.Interfaces;

namespace Maliev.PaymentService.Tests.Fixtures;

/// <summary>
/// In-memory implementation of IIdempotencyService for integration tests.
/// Replaces Redis-based implementation to avoid IConnectionMultiplexer dependency.
/// </summary>
public class TestIdempotencyService : IIdempotencyService
{
    // Key: operationType:idempotencyKey, Value: result
    private readonly ConcurrentDictionary<string, string> _store = new();
    
    // Key: operationType:idempotencyKey, Value: lockValue
    private readonly ConcurrentDictionary<string, string> _locks = new();

    private string GetKey(string operationType, string idempotencyKey)
    {
        return $"{operationType}:{idempotencyKey}";
    }

    public Task<bool> IsProcessedAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        return Task.FromResult(_store.ContainsKey(key));
    }

    public Task StoreResultAsync(string operationType, string idempotencyKey, string result, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        _store[key] = result;
        // In-memory implementation ignores TTL cleanups for simplicity
        return Task.CompletedTask;
    }

    public Task<string?> GetResultAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task<bool> AcquireLockAsync(string operationType, string idempotencyKey, TimeSpan lockTimeout, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        var lockValue = Guid.NewGuid().ToString();
        
        // TryAdd is atomic, similar to Redis SET NX
        return Task.FromResult(_locks.TryAdd(key, lockValue));
    }

    public Task ReleaseLockAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = GetKey(operationType, idempotencyKey);
        _locks.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
