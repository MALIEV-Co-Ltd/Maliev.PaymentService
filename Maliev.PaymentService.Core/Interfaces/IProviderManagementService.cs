using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;

namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Service interface for payment provider management operations.
/// Handles business logic for provider CRUD and credential encryption.
/// </summary>
public interface IProviderManagementService
{
    /// <summary>
    /// Registers a new payment provider.
    /// Encrypts credentials before storage.
    /// </summary>
    Task<PaymentProvider> RegisterProviderAsync(PaymentProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all payment providers.
    /// </summary>
    Task<IEnumerable<PaymentProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a provider by ID.
    /// Decrypts credentials if requested.
    /// </summary>
    Task<PaymentProvider?> GetProviderByIdAsync(Guid id, bool decryptCredentials = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active providers supporting a specific currency.
    /// </summary>
    Task<IEnumerable<PaymentProvider>> GetActiveByCurrencyAsync(string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a provider.
    /// Re-encrypts credentials if changed.
    /// </summary>
    Task<PaymentProvider> UpdateProviderAsync(PaymentProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a provider's status.
    /// </summary>
    Task UpdateProviderStatusAsync(Guid id, ProviderStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a provider (soft delete).
    /// </summary>
    Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default);
}
