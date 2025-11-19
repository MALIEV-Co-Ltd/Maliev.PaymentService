namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Interface for idempotency management to prevent duplicate operations.
/// Handles idempotency for both payment and refund requests using operation type + idempotency key as composite key.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an operation with the given idempotency key has already been processed.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "payment", "refund")</param>
    /// <param name="idempotencyKey">Unique idempotency key for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if already processed, false otherwise</returns>
    Task<bool> IsProcessedAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the result of an operation with distributed locking.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "payment", "refund")</param>
    /// <param name="idempotencyKey">Unique idempotency key for the operation</param>
    /// <param name="result">Serialized result to store</param>
    /// <param name="ttl">Time-to-live for the stored result (default: 24 hours)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreResultAsync(string operationType, string idempotencyKey, string result, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the stored result for an idempotency key.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "payment", "refund")</param>
    /// <param name="idempotencyKey">Unique idempotency key for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stored result or null if not found</returns>
    Task<string?> GetResultAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a distributed lock for an operation.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "payment", "refund")</param>
    /// <param name="idempotencyKey">Unique idempotency key for the operation</param>
    /// <param name="lockTimeout">Lock timeout duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock acquired, false otherwise</returns>
    Task<bool> AcquireLockAsync(string operationType, string idempotencyKey, TimeSpan lockTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a distributed lock for an operation.
    /// </summary>
    /// <param name="operationType">Type of operation (e.g., "payment", "refund")</param>
    /// <param name="idempotencyKey">Unique idempotency key for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseLockAsync(string operationType, string idempotencyKey, CancellationToken cancellationToken = default);
}
