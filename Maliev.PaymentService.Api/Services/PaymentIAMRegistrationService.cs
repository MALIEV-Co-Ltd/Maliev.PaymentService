using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.PaymentService.Api.Authorization;

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
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public PaymentIAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<PaymentIAMRegistrationService> logger)
        : base(httpClientFactory, logger, "payment")
    {
    }

    /// <summary>
    /// Gets all permissions for the Payment Service.
    /// </summary>
    /// <returns>Collection of permission registrations.</returns>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return PaymentPermissions.GetAll().Select(p => new PermissionRegistration
        {
            PermissionId = p,
            Description = GetPermissionDescription(p)
        });
    }

    /// <summary>
    /// Gets all predefined roles for the Payment Service.
    /// </summary>
    /// <returns>Collection of role registrations.</returns>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return PaymentPredefinedRoles.GetAll().Select(r => new RoleRegistration
        {
            RoleId = $"roles.payment.{r.Key}",
            Description = $"Predefined {r.Key} role for Payment Service",
            PermissionIds = r.Value,
            IsCustom = false
        });
    }

    private static string GetPermissionDescription(string permission)
    {
        return permission switch
        {
            PaymentPermissions.PaymentsCreate => "Create payment records",
            PaymentPermissions.PaymentsRead => "Read payment details",
            PaymentPermissions.PaymentsUpdate => "Update payment information",
            PaymentPermissions.PaymentsProcess => "Process payments",
            PaymentPermissions.PaymentsRefund => "Refund payments",
            PaymentPermissions.PaymentsVoid => "Void payments",
            PaymentPermissions.PaymentsReconcile => "Reconcile payment transactions",
            PaymentPermissions.TransactionsRead => "Read transaction details",
            PaymentPermissions.TransactionsQuery => "Query transaction history",
            PaymentPermissions.TransactionsExport => "Export transaction data",
            PaymentPermissions.ProvidersManage => "Manage payment providers",
            PaymentPermissions.ProvidersView => "View provider configurations",
            PaymentPermissions.ProvidersTest => "Test provider connections",
            PaymentPermissions.GatewayConfigure => "Configure payment gateway",
            PaymentPermissions.GatewayMonitor => "Monitor gateway health",
            _ => $"Permission: {permission}"
        };
    }
}
