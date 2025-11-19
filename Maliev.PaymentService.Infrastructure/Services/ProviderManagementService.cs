using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Maliev.PaymentService.Infrastructure.Encryption;

namespace Maliev.PaymentService.Infrastructure.Services;

/// <summary>
/// Implementation of provider management service.
/// Handles provider CRUD operations and credential encryption/decryption.
/// </summary>
public class ProviderManagementService : IProviderManagementService
{
    private readonly IProviderRepository _repository;
    private readonly IEncryptionService _encryptionService;

    public ProviderManagementService(
        IProviderRepository repository,
        IEncryptionService encryptionService)
    {
        _repository = repository;
        _encryptionService = encryptionService;
    }

    public async Task<PaymentProvider> RegisterProviderAsync(PaymentProvider provider, CancellationToken cancellationToken = default)
    {
        // Encrypt all credentials before storage
        var encryptedCredentials = new Dictionary<string, string>();
        foreach (var (key, value) in provider.Credentials)
        {
            encryptedCredentials[key] = _encryptionService.Encrypt(value);
        }
        provider.Credentials = encryptedCredentials;

        return await _repository.AddAsync(provider, cancellationToken);
    }

    public async Task<IEnumerable<PaymentProvider>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }

    public async Task<PaymentProvider?> GetProviderByIdAsync(Guid id, bool decryptCredentials = true, CancellationToken cancellationToken = default)
    {
        var provider = await _repository.GetByIdAsync(id, cancellationToken);

        if (provider != null && decryptCredentials && provider.Credentials.Count > 0)
        {
            // Decrypt credentials for use
            var decryptedCredentials = new Dictionary<string, string>();
            foreach (var (key, encryptedValue) in provider.Credentials)
            {
                decryptedCredentials[key] = _encryptionService.Decrypt(encryptedValue);
            }
            provider.Credentials = decryptedCredentials;
        }

        return provider;
    }

    public async Task<IEnumerable<PaymentProvider>> GetActiveByCurrencyAsync(string currency, CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveByCurrencyAsync(currency, cancellationToken);
    }

    public async Task<PaymentProvider> UpdateProviderAsync(PaymentProvider provider, CancellationToken cancellationToken = default)
    {
        // Re-encrypt credentials if they've been changed
        if (provider.Credentials.Count > 0)
        {
            var encryptedCredentials = new Dictionary<string, string>();
            foreach (var (key, value) in provider.Credentials)
            {
                // Check if value looks encrypted (basic heuristic - in real scenario, you'd have a better check)
                if (!value.StartsWith("encrypted_"))
                {
                    encryptedCredentials[key] = _encryptionService.Encrypt(value);
                }
                else
                {
                    encryptedCredentials[key] = value;
                }
            }
            provider.Credentials = encryptedCredentials;
        }

        return await _repository.UpdateAsync(provider, cancellationToken);
    }

    public async Task UpdateProviderStatusAsync(Guid id, ProviderStatus status, CancellationToken cancellationToken = default)
    {
        var provider = await _repository.GetByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider with ID {id} not found");
        }

        provider.Status = status;
        await _repository.UpdateAsync(provider, cancellationToken);
    }

    public async Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
    }
}
