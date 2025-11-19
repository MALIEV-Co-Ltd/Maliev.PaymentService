using Maliev.PaymentService.Core.Entities;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Repository interface for PaymentProvider entity operations.
/// Provides data access methods for provider management.
/// </summary>
public interface IProviderRepository
{
    /// <summary>
    /// Gets all payment providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all providers (excluding soft-deleted)</returns>
    Task<IEnumerable<PaymentProvider>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment provider by its unique identifier.
    /// </summary>
    /// <param name="id">Provider ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provider if found, null otherwise</returns>
    Task<PaymentProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a payment provider by name.
    /// </summary>
    /// <param name="name">Provider name (e.g., "stripe", "paypal")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provider if found, null otherwise</returns>
    Task<PaymentProvider?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active providers that support a specific currency.
    /// Ordered by priority (ascending).
    /// </summary>
    /// <param name="currency">ISO 4217 currency code (e.g., "USD", "EUR")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active providers supporting the currency</returns>
    Task<IEnumerable<PaymentProvider>> GetActiveByCurrencyAsync(string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new payment provider.
    /// </summary>
    /// <param name="provider">Provider to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Added provider</returns>
    Task<PaymentProvider> AddAsync(PaymentProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing payment provider.
    /// </summary>
    /// <param name="provider">Provider with updated data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated provider</returns>
    Task<PaymentProvider> UpdateAsync(PaymentProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a payment provider (soft delete).
    /// </summary>
    /// <param name="id">Provider ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
