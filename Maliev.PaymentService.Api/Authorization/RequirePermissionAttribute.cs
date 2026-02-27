using Microsoft.AspNetCore.Authorization;

namespace Maliev.PaymentService.Api.Authorization;

/// <summary>
/// Specifies that the class or method that this attribute is applied to requires a specific permission.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Gets the required permission.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The required permission.</param>
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = $"Permission:{permission}";
        AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    }
}
