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
    /// Registers a new payment provider with the gateway.
    /// </summary>
    /// <param name="request">The provider registration request containing name, credentials, and configuration.</param>
    /// <returns>The registered provider details including ID and configuration.</returns>
    /// <remarks>
    /// Registers a new payment provider (e.g., Stripe, PayPal, SCB, Omise) with the payment gateway.
    /// Provider credentials are automatically encrypted using ASP.NET Core Data Protection.
    ///
    /// **Required Headers:**
    /// - `Authorization`: Bearer token for authentication (admin role required)
    ///
    /// **Supported Providers:**
    /// - `stripe`: Stripe payment processor
    /// - `paypal`: PayPal payment processor
    /// - `scb`: Siam Commercial Bank API
    /// - `omise`: Omise payment gateway (Thailand)
    ///
    /// **Configuration:**
    /// - Provider can have multiple regional configurations
    /// - Each configuration includes API base URL, timeouts, retry policies
    /// - Credentials are stored encrypted in the database
    ///
    /// **Example Request:**
    /// ```json
    /// {
    ///   "name": "stripe",
    ///   "displayName": "Stripe Payments",
    ///   "status": "active",
    ///   "supportedCurrencies": ["USD", "EUR", "THB"],
    ///   "priority": 1,
    ///   "credentials": {
    ///     "apiKey": "sk_test_...",
    ///     "webhookSecret": "whsec_..."
    ///   },
    ///   "configurations": [{
    ///     "region": "global",
    ///     "apiBaseUrl": "https://api.stripe.com",
    ///     "isActive": true,
    ///     "maxRetries": 3,
    ///     "timeoutSeconds": 30
    ///   }]
    /// }
    /// ```
    /// </remarks>
    /// <response code="201">Provider successfully registered. Returns provider details.</response>
    /// <response code="400">Invalid request. Missing required fields or invalid configuration.</response>
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
    /// Gets all registered payment providers with their current status.
    /// </summary>
    /// <returns>A list of all payment providers with summary information.</returns>
    /// <remarks>
    /// Retrieves a list of all payment providers registered in the system, including their current
    /// status, supported currencies, priority, and health metrics.
    ///
    /// **Required Headers:**
    /// - `Authorization`: Bearer token for authentication
    ///
    /// **Provider Status:**
    /// - `active`: Provider is enabled and accepting payments
    /// - `inactive`: Provider is disabled (not used for new payments)
    /// - `maintenance`: Provider is temporarily unavailable
    /// - `circuit_open`: Circuit breaker triggered (automatic recovery)
    ///
    /// **Use Cases:**
    /// - Admin dashboard for provider management
    /// - Health monitoring and alerting
    /// - Provider selection UI for customers
    /// - Integration testing and validation
    ///
    /// **Example Response:**
    /// ```json
    /// [
    ///   {
    ///     "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "name": "stripe",
    ///     "displayName": "Stripe Payments",
    ///     "status": "active",
    ///     "supportedCurrencies": ["USD", "EUR", "THB"],
    ///     "priority": 1,
    ///     "healthScore": 0.98,
    ///     "lastHealthCheck": "2025-11-19T12:30:00Z"
    ///   }
    /// ]
    /// ```
    /// </remarks>
    /// <response code="200">Returns list of all payment providers with summary information.</response>
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
