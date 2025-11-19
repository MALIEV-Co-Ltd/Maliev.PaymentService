using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// API controller for payment provider management.
/// Handles provider registration, updates, and queries.
/// </summary>
[ApiController]
[Route("payments/v1/providers")]
[Authorize]
public class ProvidersController : ControllerBase
{
    private readonly IProviderManagementService _providerService;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(
        IProviderManagementService providerService,
        ILogger<ProvidersController> logger)
    {
        _providerService = providerService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new payment provider.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProviderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProviderResponse>> RegisterProvider([FromBody] RegisterProviderRequest request)
    {
        var provider = new PaymentProvider
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            Status = request.Status,
            SupportedCurrencies = request.SupportedCurrencies,
            Priority = request.Priority,
            Credentials = request.Credentials,
            Configurations = request.Configurations.Select(c => new ProviderConfiguration
            {
                Id = Guid.NewGuid(),
                PaymentProviderId = Guid.Empty, // Will be set by EF Core
                Region = c.Region,
                ApiBaseUrl = c.ApiBaseUrl,
                IsActive = c.IsActive,
                MaxRetries = c.MaxRetries,
                TimeoutSeconds = c.TimeoutSeconds,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _providerService.RegisterProviderAsync(provider);

        _logger.LogInformation("Registered new provider: {ProviderName} (ID: {ProviderId})", result.Name, result.Id);

        return CreatedAtAction(nameof(GetProviderById), new { id = result.Id }, MapToResponse(result));
    }

    /// <summary>
    /// Gets all payment providers.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProviderSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProviderSummary>>> GetAllProviders()
    {
        var providers = await _providerService.GetAllProvidersAsync();
        return Ok(providers.Select(MapToSummary));
    }

    /// <summary>
    /// Gets active providers that support a specific currency.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<ProviderSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProviderSummary>>> GetActiveByCurrency([FromQuery] string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return BadRequest("Currency parameter is required");
        }

        var providers = await _providerService.GetActiveByCurrencyAsync(currency.ToUpperInvariant());
        return Ok(providers.Select(MapToSummary));
    }

    /// <summary>
    /// Gets a specific provider by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProviderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderResponse>> GetProviderById(Guid id)
    {
        var provider = await _providerService.GetProviderByIdAsync(id, decryptCredentials: false);

        if (provider == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(provider));
    }

    /// <summary>
    /// Updates a payment provider.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ProviderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderResponse>> UpdateProvider(Guid id, [FromBody] UpdateProviderRequest request)
    {
        var existingProvider = await _providerService.GetProviderByIdAsync(id, decryptCredentials: true);

        if (existingProvider == null)
        {
            return NotFound();
        }

        // Update only provided fields
        if (request.DisplayName != null)
            existingProvider.DisplayName = request.DisplayName;

        if (request.Status.HasValue)
            existingProvider.Status = request.Status.Value;

        if (request.SupportedCurrencies != null)
            existingProvider.SupportedCurrencies = request.SupportedCurrencies;

        if (request.Priority.HasValue)
            existingProvider.Priority = request.Priority.Value;

        if (request.Credentials != null)
            existingProvider.Credentials = request.Credentials;

        existingProvider.UpdatedAt = DateTime.UtcNow;

        var result = await _providerService.UpdateProviderAsync(existingProvider);

        _logger.LogInformation("Updated provider: {ProviderName} (ID: {ProviderId})", result.Name, result.Id);

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Updates a provider's operational status.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProviderStatus(Guid id, [FromBody] UpdateProviderStatusRequest request)
    {
        try
        {
            await _providerService.UpdateProviderStatusAsync(id, request.Status);

            _logger.LogInformation("Updated provider status: ID {ProviderId} to {Status}", id, request.Status);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a payment provider (soft delete).
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteProvider(Guid id)
    {
        await _providerService.DeleteProviderAsync(id);

        _logger.LogInformation("Deleted provider: ID {ProviderId}", id);

        return NoContent();
    }

    private static ProviderResponse MapToResponse(PaymentProvider provider)
    {
        return new ProviderResponse
        {
            Id = provider.Id,
            Name = provider.Name,
            DisplayName = provider.DisplayName,
            Status = provider.Status,
            SupportedCurrencies = provider.SupportedCurrencies,
            Priority = provider.Priority,
            Configurations = provider.Configurations.Select(c => new ProviderResponse.ProviderConfigurationDto
            {
                Id = c.Id,
                Region = c.Region,
                ApiBaseUrl = c.ApiBaseUrl,
                IsActive = c.IsActive,
                MaxRetries = c.MaxRetries,
                TimeoutSeconds = c.TimeoutSeconds
            }).ToList(),
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }

    private static ProviderSummary MapToSummary(PaymentProvider provider)
    {
        return new ProviderSummary
        {
            Id = provider.Id,
            Name = provider.Name,
            DisplayName = provider.DisplayName,
            Status = provider.Status,
            SupportedCurrencies = provider.SupportedCurrencies,
            Priority = provider.Priority
        };
    }
}
