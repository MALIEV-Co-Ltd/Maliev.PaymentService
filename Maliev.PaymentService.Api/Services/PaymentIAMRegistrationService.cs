using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.PaymentService.Application.Authorization;

namespace Maliev.PaymentService.Api.Services;

/// <summary>
/// Background service that registers Payment Service permissions and roles with IAM.
/// Uses the standard IAMRegistrationService base class.
/// </summary>
public class PaymentIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public PaymentIAMRegistrationService(
        IConfiguration configuration,
        ILogger<PaymentIAMRegistrationService> logger)
        : base(configuration, logger, "payment")
    {
    }

    /// <summary>
    /// Gets all permissions for the Payment Service.
    /// </summary>
    /// <returns>Collection of permission registrations.</returns>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return PaymentPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <summary>
    /// Gets all predefined roles for the Payment Service.
    /// </summary>
    /// <returns>Collection of role registrations.</returns>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return PaymentPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
