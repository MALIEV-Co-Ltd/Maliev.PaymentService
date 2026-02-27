using Microsoft.AspNetCore.Authorization;

namespace Maliev.PaymentService.Api.Authorization;

/// <summary>
/// Represents a requirement that a user must have a specific permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the required permission.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The required permission.</param>
    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
