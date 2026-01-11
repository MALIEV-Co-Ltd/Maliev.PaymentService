using System.Collections.Frozen;
using System.Collections.Generic;

namespace Maliev.PaymentService.Api.Authorization;

/// <summary>
/// Defines granular permissions for the Payment Service.
/// Following format: payment.{resource}.{action}
/// </summary>
public static class PaymentPermissions
{
    /// <summary>Permission to create payment records.</summary>
    public const string PaymentsCreate = "payment.payments.create";
    /// <summary>Permission to read payment details.</summary>
    public const string PaymentsRead = "payment.payments.read";
    /// <summary>Permission to update payment information.</summary>
    public const string PaymentsUpdate = "payment.payments.update";
    /// <summary>Permission to process payments (critical).</summary>
    public const string PaymentsProcess = "payment.payments.process";
    /// <summary>Permission to refund payments (critical).</summary>
    public const string PaymentsRefund = "payment.payments.refund";
    /// <summary>Permission to void payments (critical).</summary>
    public const string PaymentsVoid = "payment.payments.void";
    /// <summary>Permission to reconcile payment transactions.</summary>
    public const string PaymentsReconcile = "payment.payments.reconcile";

    /// <summary>Permission to read transaction details.</summary>
    public const string TransactionsRead = "payment.transactions.read";
    /// <summary>Permission to query transaction history.</summary>
    public const string TransactionsQuery = "payment.transactions.query";
    /// <summary>Permission to export transaction data.</summary>
    public const string TransactionsExport = "payment.transactions.export";

    /// <summary>Permission to manage payment providers.</summary>
    public const string ProvidersManage = "payment.providers.manage";
    /// <summary>Permission to view provider configurations.</summary>
    public const string ProvidersView = "payment.providers.view";
    /// <summary>Permission to test provider connections.</summary>
    public const string ProvidersTest = "payment.providers.test";

    /// <summary>Permission to configure payment gateway.</summary>
    public const string GatewayConfigure = "payment.gateway.configure";
    /// <summary>Permission to monitor gateway health.</summary>
    public const string GatewayMonitor = "payment.gateway.monitor";

    /// <summary>
    /// Collection of all defined payment permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { PaymentsCreate, "Create payment records" },
        { PaymentsRead, "Read payment details" },
        { PaymentsUpdate, "Update payment information" },
        { PaymentsProcess, "Process payments" },
        { PaymentsRefund, "Refund payments" },
        { PaymentsVoid, "Void payments" },
        { PaymentsReconcile, "Reconcile payment transactions" },
        { TransactionsRead, "Read transaction details" },
        { TransactionsQuery, "Query transaction history" },
        { TransactionsExport, "Export transaction data" },
        { ProvidersManage, "Manage payment providers" },
        { ProvidersView, "View provider configurations" },
        { ProvidersTest, "Test provider connections" },
        { GatewayConfigure, "Configure payment gateway" },
        { GatewayMonitor, "Monitor gateway health" }
    };

    /// <summary>
    /// Gets all defined permissions using reflection to ensure none are missed.
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        return typeof(PaymentPermissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!);
    }

    /// <summary>
    /// Collection of permissions that require real-time revocation checking.
    /// </summary>
    public static readonly IReadOnlySet<string> CriticalPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PaymentsProcess,
        PaymentsRefund,
        PaymentsVoid
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
