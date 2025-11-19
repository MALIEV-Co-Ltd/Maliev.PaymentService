using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Maliev.PaymentService.Infrastructure.Caching;

/// <summary>
/// Redis-based implementation of IIdempotencyService with distributed locking.
/// Uses operation type + idempotency key as composite key with 24-hour TTL.
/// </summary>
public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _instanceName;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    public RedisIdempotencyService(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis;
        _instanceName = configuration["Redis:InstanceName"] ?? "PaymentGateway:";
    }

    private string GetKey(string operationType, string idempotencyKey)
    {
        return $"{_instanceName}idempotency:{operationType}:{idempotencyKey}";
    }

    private string GetLockKey(string operationType, string idempotencyKey)
    {
        return $"{_instanceName}lock:{operationType}:{idempotencyKey}";
    }

    public async Task<bool> IsProcessedAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(operationType, idempotencyKey);
        return await db.KeyExistsAsync(key);
    }

    public async Task StoreResultAsync(string operationType, string idempotencyKey, string result, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(operationType, idempotencyKey);
        var expiry = ttl ?? _defaultTtl;
        await db.StringSetAsync(key, result, expiry);
    }

    public async Task<string?> GetResultAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetKey(operationType, idempotencyKey);
        var value = await db.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<bool> AcquireLockAsync(string operationType, string idempotencyKey, TimeSpan lockTimeout, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = GetLockKey(operationType, idempotencyKey);
        var lockValue = Guid.NewGuid().ToString();

        // Try to acquire lock with NX (only if not exists) and expiry
        return await db.StringSetAsync(lockKey, lockValue, lockTimeout, When.NotExists);
    }

    public async Task ReleaseLockAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockKey = GetLockKey(operationType, idempotencyKey);
        await db.KeyDeleteAsync(lockKey);
    }
}
