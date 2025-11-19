using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PaymentService.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for PaymentProvider entity.
/// Provides data access using EF Core and PostgreSQL.
/// </summary>
public class ProviderRepository : IProviderRepository
{
    private readonly PaymentDbContext _context;

    public ProviderRepository(PaymentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all payment providers with their configurations.
    /// </summary>
    public async Task<IEnumerable<PaymentProvider>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PaymentProviders
            .Include(p => p.Configurations)
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a payment provider by ID with its configurations.
    /// </summary>
    public async Task<PaymentProvider?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentProviders
            .Include(p => p.Configurations)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets a payment provider by name with its configurations.
    /// </summary>
    public async Task<PaymentProvider?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentProviders
            .Include(p => p.Configurations)
            .FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    /// <summary>
    /// Gets active providers supporting a specific currency, ordered by priority.
    /// </summary>
    public async Task<IEnumerable<PaymentProvider>> GetActiveByCurrencyAsync(string currency, CancellationToken cancellationToken = default)
    {
        return await _context.PaymentProviders
            .Include(p => p.Configurations)
            .Where(p => p.Status == ProviderStatus.Active && p.SupportedCurrencies.Contains(currency))
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new payment provider.
    /// </summary>
    public async Task<PaymentProvider> AddAsync(PaymentProvider provider, CancellationToken cancellationToken = default)
    {
        await _context.PaymentProviders.AddAsync(provider, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return provider;
    }

    /// <summary>
    /// Updates an existing payment provider.
    /// </summary>
    public async Task<PaymentProvider> UpdateAsync(PaymentProvider provider, CancellationToken cancellationToken = default)
    {
        provider.UpdatedAt = DateTime.UtcNow;
        _context.PaymentProviders.Update(provider);
        await _context.SaveChangesAsync(cancellationToken);
        return provider;
    }

    /// <summary>
    /// Soft deletes a payment provider.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await GetByIdAsync(id, cancellationToken);
        if (provider != null)
        {
            provider.DeletedAt = DateTime.UtcNow;
            provider.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
