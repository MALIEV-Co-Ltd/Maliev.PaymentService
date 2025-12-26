namespace Maliev.PaymentService.Api.Authorization;

/// <summary>
/// Defines predefined roles and their associated permissions for the Payment Service.
/// </summary>
public static class PaymentPredefinedRoles
{
    /// <summary>Full administrative access to all payment operations.</summary>
    public const string Admin = "payment-admin";
    /// <summary>Can process, refund, and void payments.</summary>
    public const string Processor = "payment-processor";
    /// <summary>Can reconcile and export payment data.</summary>
    public const string Accountant = "payment-accountant";
    /// <summary>Read-only access to payments.</summary>
    public const string Viewer = "payment-viewer";
    /// <summary>Can manage providers and monitor gateway.</summary>
    public const string Operations = "payment-operations";

    private static readonly Dictionary<string, List<string>> RolePermissions = new()
    {
        {
            Admin, new List<string> { "payment.*" }
        },
        {
            Processor, new List<string>
            {
                PaymentPermissions.PaymentsCreate,
                PaymentPermissions.PaymentsRead,
                PaymentPermissions.PaymentsProcess,
                PaymentPermissions.PaymentsRefund,
                PaymentPermissions.PaymentsVoid,
                PaymentPermissions.TransactionsRead
            }
        },
        {
            Accountant, new List<string>
            {
                PaymentPermissions.PaymentsRead,
                PaymentPermissions.PaymentsReconcile,
                PaymentPermissions.TransactionsRead,
                PaymentPermissions.TransactionsQuery,
                PaymentPermissions.TransactionsExport
            }
        },
        {
            Viewer, new List<string>
            {
                PaymentPermissions.PaymentsRead,
                PaymentPermissions.TransactionsRead
            }
        },
        {
            Operations, new List<string>
            {
                PaymentPermissions.PaymentsRead,
                PaymentPermissions.ProvidersManage,
                PaymentPermissions.ProvidersView,
                PaymentPermissions.ProvidersTest,
                PaymentPermissions.GatewayConfigure,
                PaymentPermissions.GatewayMonitor
            }
        }
    };

    /// <summary>
    /// Gets the permissions associated with a role.
    /// </summary>
    public static IReadOnlyList<string> GetPermissions(string roleName)
    {
        return RolePermissions.TryGetValue(roleName, out var permissions)
            ? permissions.AsReadOnly()
            : new List<string>().AsReadOnly();
    }

    /// <summary>
    /// Gets all defined roles and their permissions.
    /// </summary>
    public static IReadOnlyDictionary<string, List<string>> GetAll() => RolePermissions;
}