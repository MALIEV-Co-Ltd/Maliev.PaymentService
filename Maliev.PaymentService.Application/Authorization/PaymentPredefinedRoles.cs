namespace Maliev.PaymentService.Application.Authorization;

/// <summary>
/// Predefined roles for the Payment Service.
/// Roles follow the GCP format: roles.payment.{role-name}
/// </summary>
public static class PaymentPredefinedRoles
{
    /// <summary>Role for administrators with full access.</summary>
    public const string Admin = "roles.payment.admin";
    /// <summary>Role for payment processors.</summary>
    public const string Processor = "roles.payment.processor";
    /// <summary>Role for accountants with reconciliation access.</summary>
    public const string Accountant = "roles.payment.accountant";
    /// <summary>Role for users with read-only access.</summary>
    public const string Viewer = "roles.payment.viewer";
    /// <summary>Role for operations managing providers and gateway.</summary>
    public const string Operations = "roles.payment.operations";

    /// <summary>
    /// Represents a predefined role definition.
    /// </summary>
    public record RoleDefinition(string RoleId, string Description, string[] Permissions);

    /// <summary>
    /// Collection of all predefined roles for the Payment Service.
    /// </summary>
    public static readonly IReadOnlyList<RoleDefinition> All = new List<RoleDefinition>
    {
        new(Admin, "Full administrative access to all payment operations", PaymentPermissions.GetAll().ToArray()),

        new(Processor, "Can process, refund, and void payments", new[]
        {
            PaymentPermissions.PaymentsCreate,
            PaymentPermissions.PaymentsRead,
            PaymentPermissions.PaymentsProcess,
            PaymentPermissions.PaymentsRefund,
            PaymentPermissions.PaymentsVoid,
            PaymentPermissions.TransactionsRead
        }),

        new(Accountant, "Can reconcile and export payment data", new[]
        {
            PaymentPermissions.PaymentsRead,
            PaymentPermissions.PaymentsReconcile,
            PaymentPermissions.TransactionsRead,
            PaymentPermissions.TransactionsQuery,
            PaymentPermissions.TransactionsExport
        }),

        new(Viewer, "Read-only access to payments", new[]
        {
            PaymentPermissions.PaymentsRead,
            PaymentPermissions.TransactionsRead
        }),

        new(Operations, "Can manage providers and monitor gateway", new[]
        {
            PaymentPermissions.PaymentsRead,
            PaymentPermissions.ProvidersManage,
            PaymentPermissions.ProvidersView,
            PaymentPermissions.ProvidersTest,
            PaymentPermissions.GatewayConfigure,
            PaymentPermissions.GatewayMonitor
        })
    };

    /// <summary>
    /// Gets the permissions associated with a role name.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>Array of permission IDs.</returns>
    public static string[] GetPermissions(string roleId)
    {
        return All.FirstOrDefault(r => r.RoleId == roleId)?.Permissions ?? Array.Empty<string>();
    }
}
