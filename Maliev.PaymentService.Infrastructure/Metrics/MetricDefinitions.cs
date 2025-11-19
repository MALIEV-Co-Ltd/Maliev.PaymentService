using Prometheus;

namespace Maliev.PaymentService.Infrastructure.Metrics;

/// <summary>
/// Prometheus metric definitions for the Payment Gateway Service.
/// Defines counters, gauges, and histograms for operational observability.
/// </summary>
public static class MetricDefinitions
{
    private const string Namespace = "payment_gateway";

    /// <summary>
    /// Counter for total payment transactions.
    /// Labels: service_name, provider, status, currency, environment, version
    /// </summary>
    public static readonly Counter PaymentTransactionsTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_payment_transactions_total",
        "Total number of payment transactions",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "status", "currency", "environment", "version" }
        });

    /// <summary>
    /// Counter for total refund transactions.
    /// Labels: service_name, provider, status, environment, version
    /// </summary>
    public static readonly Counter RefundTransactionsTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_refund_transactions_total",
        "Total number of refund transactions",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "status", "environment", "version" }
        });

    /// <summary>
    /// Counter for webhook events processed.
    /// Labels: service_name, provider, event_type, success, environment, version
    /// </summary>
    public static readonly Counter WebhooksProcessedTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_webhooks_processed_total",
        "Total number of webhook events processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "event_type", "success", "environment", "version" }
        });

    /// <summary>
    /// Counter for webhook signature validation failures.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Counter WebhookValidationFailuresTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_webhook_validation_failures_total",
        "Total number of webhook signature validation failures",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Histogram for webhook processing duration in seconds.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Histogram WebhookDurationSeconds = Prometheus.Metrics.CreateHistogram(
        $"{Namespace}_webhook_duration_seconds",
        "Webhook processing duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" },
            Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
        });

    /// <summary>
    /// Histogram for payment processing duration in seconds.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Histogram PaymentDurationSeconds = Prometheus.Metrics.CreateHistogram(
        $"{Namespace}_payment_duration_seconds",
        "Payment processing duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" },
            Buckets = new[] { 0.1, 0.5, 1.0, 2.0, 3.0, 5.0, 10.0 }
        });

    /// <summary>
    /// Gauge for active concurrent requests.
    /// Labels: service_name, environment, version
    /// </summary>
    public static readonly Gauge ActiveRequestsGauge = Prometheus.Metrics.CreateGauge(
        $"{Namespace}_active_requests",
        "Number of active concurrent requests",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service_name", "environment", "version" }
        });

    /// <summary>
    /// Gauge for provider health score (0.0 to 1.0).
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Gauge ProviderHealthGauge = Prometheus.Metrics.CreateGauge(
        $"{Namespace}_provider_health",
        "Provider health score (0.0 to 1.0)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Counter for reconciliation discrepancies detected.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Counter ReconciliationDiscrepanciesTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_reconciliation_discrepancies_total",
        "Total number of reconciliation discrepancies detected",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Gauge for last reconciliation run timestamp (Unix epoch seconds).
    /// Labels: service_name, environment, version
    /// </summary>
    public static readonly Gauge ReconciliationLastRunTimestamp = Prometheus.Metrics.CreateGauge(
        $"{Namespace}_reconciliation_last_run_timestamp",
        "Timestamp of last reconciliation run (Unix epoch seconds)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service_name", "environment", "version" }
        });

    /// <summary>
    /// Counter for total transactions checked during reconciliation.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Counter ReconciliationTransactionsCheckedTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_reconciliation_transactions_checked_total",
        "Total number of transactions checked during reconciliation",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Counter for total payment status queries.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Counter PaymentStatusQueriesTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_payment_status_queries_total",
        "Total number of payment status queries",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Counter for payment status cache hits.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Counter PaymentStatusCacheHitsTotal = Prometheus.Metrics.CreateCounter(
        $"{Namespace}_payment_status_cache_hits_total",
        "Total number of payment status cache hits",
        new CounterConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" }
        });

    /// <summary>
    /// Histogram for payment status query duration in seconds.
    /// Labels: service_name, provider, environment, version
    /// </summary>
    public static readonly Histogram PaymentStatusQueryDurationSeconds = Prometheus.Metrics.CreateHistogram(
        $"{Namespace}_payment_status_query_duration_seconds",
        "Payment status query duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service_name", "provider", "environment", "version" },
            Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
        });
}
