namespace Maliev.PaymentService.Core.Interfaces;

/// <summary>
/// Interface for metrics collection and reporting.
/// Abstracts Prometheus metrics implementation to maintain clean architecture.
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Records a payment transaction.
    /// </summary>
    void RecordPaymentTransaction(string providerName, string status, decimal amount, string currency);

    /// <summary>
    /// Records a refund transaction.
    /// </summary>
    void RecordRefundTransaction(string providerName, string status, decimal amount);

    /// <summary>
    /// Records webhook processing.
    /// </summary>
    void RecordWebhookProcessed(string providerName, string eventType, bool success);

    /// <summary>
    /// Records webhook validation failure.
    /// </summary>
    void RecordWebhookValidationFailure(string providerName);

    /// <summary>
    /// Records webhook processing duration.
    /// </summary>
    void RecordWebhookDuration(string providerName, double durationSeconds);

    /// <summary>
    /// Records payment processing duration.
    /// </summary>
    void RecordPaymentDuration(string providerName, double durationSeconds);

    /// <summary>
    /// Records active concurrent requests.
    /// </summary>
    void IncrementActiveRequests();

    /// <summary>
    /// Decrements active concurrent requests.
    /// </summary>
    void DecrementActiveRequests();

    /// <summary>
    /// Updates provider health score (0.0 to 1.0).
    /// </summary>
    void UpdateProviderHealth(string providerName, double healthScore);

    /// <summary>
    /// Records reconciliation discrepancies.
    /// </summary>
    void RecordReconciliationDiscrepancy(string providerName);

    /// <summary>
    /// Updates last reconciliation timestamp.
    /// </summary>
    void UpdateReconciliationTimestamp();

    /// <summary>
    /// Records total transactions checked during reconciliation.
    /// </summary>
    void RecordReconciliationTransactionsChecked(string providerName, int count);

    /// <summary>
    /// Records payment status query.
    /// </summary>
    void RecordPaymentStatusQuery(string providerName, double durationSeconds);

    /// <summary>
    /// Records cache hit for payment status query.
    /// </summary>
    void RecordPaymentStatusCacheHit(string providerName);
}
