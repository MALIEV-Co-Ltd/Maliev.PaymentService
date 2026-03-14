using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.PaymentService.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace Maliev.PaymentService.Api.Authorization;

/// <summary>
/// Authorization handler for permission-based requirements.
/// Implements caching for non-critical permissions and real-time revocation for critical ones.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="cache">Distributed cache for permission storage.</param>
    /// <param name="logger">Logger for authorization events.</param>
    public PermissionAuthorizationHandler(IDistributedCache cache, ILogger<PermissionAuthorizationHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Handles the permission requirement.
    /// </summary>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                     user.FindFirst("sub")?.Value ?? "unknown";

        // Check if permission is critical
        bool isCritical = PaymentPermissions.CriticalPermissions.Contains(requirement.Permission);

        if (isCritical)
        {
            // Real-time revocation checking for critical permissions
            // In a real implementation, this would call IAM service or check a revocation list
            // For now, we'll check if there's a revocation flag in cache
            var revocationKey = $"revoked:user:{userId}:permission:{requirement.Permission}";
            var isRevoked = await _cache.GetStringAsync(revocationKey);

            if (!string.IsNullOrEmpty(isRevoked))
            {
                _logger.LogWarning("Authorization failed: Permission {Permission} revoked for User {UserId}",
                    requirement.Permission, userId);
                return;
            }
        }

        // Check cache for non-critical permissions or as a secondary check
        var cacheKey = $"user:{userId}:permissions";
        var cachedPermissionsJson = await _cache.GetStringAsync(cacheKey);
        List<string>? permissions = null;

        if (!string.IsNullOrEmpty(cachedPermissionsJson))
        {
            permissions = JsonSerializer.Deserialize<List<string>>(cachedPermissionsJson);
        }

        if (permissions == null)
        {
            // If not in cache, extract from JWT claims
            permissions = user.FindAll("permissions").Select(c => c.Value).ToList();

            if (!permissions.Any())
            {
                // Fallback to "scope" if permissions claim is missing
                permissions = user.FindAll("scope").Select(c => c.Value).ToList();
            }

            // Cache the permissions for a short period (e.g., 5 minutes)
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
        }

        if (permissions.Contains(requirement.Permission) || permissions.Contains("payment.*"))
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogWarning("Authorization failed: User {UserId} lacks permission {Permission}",
            userId, requirement.Permission);
    }
}
