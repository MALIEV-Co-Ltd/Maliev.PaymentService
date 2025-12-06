using System.Diagnostics.Metrics;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.PaymentService.Infrastructure.Metrics;

/// <summary>
/// OpenTelemetry implementation of IMetricsService for metrics collection and reporting.
/// </summary>
public class PrometheusMetricsService : IMetricsService, IDisposable
{
    private readonly Meter _meter;
    private readonly string _serviceName;
    private readonly string _environment;
    private readonly string _version;

    // Counters
    private readonly Counter<long> _paymentTransactionsTotal;
    private readonly Counter<long> _refundTransactionsTotal;
    private readonly Counter<long> _webhooksProcessedTotal;
    private readonly Counter<long> _webhookValidationFailuresTotal;
    private readonly Counter<long> _reconciliationDiscrepanciesTotal;
    private readonly Counter<long> _reconciliationTransactionsCheckedTotal;
    private readonly Counter<long> _paymentStatusQueriesTotal;
    private readonly Counter<long> _paymentStatusCacheHitsTotal;

    // Histograms
    private readonly Histogram<double> _webhookDurationSeconds;
    private readonly Histogram<double> _paymentDurationSeconds;
    private readonly Histogram<double> _paymentStatusQueryDurationSeconds;

    // Gauges (using tracked state)
    private long _activeRequests;
    private long _reconciliationLastRunTimestamp;
    private readonly Dictionary<string, double> _providerHealthScores = new();

    public PrometheusMetricsService(IConfiguration configuration)
    {
        _serviceName = "payment-gateway-service";
        _environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        _version = configuration["ServiceVersion"] ?? "1.0.0";

        _meter = new Meter("payment-gateway", "1.0.0");

        // Initialize counters
        _paymentTransactionsTotal = _meter.CreateCounter<long>(
            "payment_gateway.payment_transactions_total",
            description: "Total number of payment transactions");

        _refundTransactionsTotal = _meter.CreateCounter<long>(
            "payment_gateway.refund_transactions_total",
            description: "Total number of refund transactions");

        _webhooksProcessedTotal = _meter.CreateCounter<long>(
            "payment_gateway.webhooks_processed_total",
            description: "Total number of webhook events processed");

        _webhookValidationFailuresTotal = _meter.CreateCounter<long>(
            "payment_gateway.webhook_validation_failures_total",
            description: "Total number of webhook signature validation failures");

        _reconciliationDiscrepanciesTotal = _meter.CreateCounter<long>(
            "payment_gateway.reconciliation_discrepancies_total",
            description: "Total number of reconciliation discrepancies detected");

        _reconciliationTransactionsCheckedTotal = _meter.CreateCounter<long>(
            "payment_gateway.reconciliation_transactions_checked_total",
            description: "Total number of transactions checked during reconciliation");

        _paymentStatusQueriesTotal = _meter.CreateCounter<long>(
            "payment_gateway.payment_status_queries_total",
            description: "Total number of payment status queries");

        _paymentStatusCacheHitsTotal = _meter.CreateCounter<long>(
            "payment_gateway.payment_status_cache_hits_total",
            description: "Total number of payment status cache hits");

        // Initialize histograms
        _webhookDurationSeconds = _meter.CreateHistogram<double>(
            "payment_gateway.webhook_duration_seconds",
            unit: "s",
            description: "Webhook processing duration in seconds");

        _paymentDurationSeconds = _meter.CreateHistogram<double>(
            "payment_gateway.payment_duration_seconds",
            unit: "s",
            description: "Payment processing duration in seconds");

        _paymentStatusQueryDurationSeconds = _meter.CreateHistogram<double>(
            "payment_gateway.payment_status_query_duration_seconds",
            unit: "s",
            description: "Payment status query duration in seconds");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "payment_gateway.active_requests",
            () => new Measurement<long>(_activeRequests,
                new KeyValuePair<string, object?>("service_name", _serviceName),
                new KeyValuePair<string, object?>("environment", _environment),
                new KeyValuePair<string, object?>("version", _version)),
            description: "Number of active concurrent requests");

        _meter.CreateObservableGauge(
            "payment_gateway.provider_health",
            () =>
            {
                var measurements = new List<Measurement<double>>();
                lock (_providerHealthScores)
                {
                    foreach (var kvp in _providerHealthScores)
                    {
                        measurements.Add(new Measurement<double>(kvp.Value,
                            new KeyValuePair<string, object?>("service_name", _serviceName),
                            new KeyValuePair<string, object?>("provider", kvp.Key),
                            new KeyValuePair<string, object?>("environment", _environment),
                            new KeyValuePair<string, object?>("version", _version)));
                    }
                }
                return measurements;
            },
            description: "Provider health score (0.0 to 1.0)");

        _meter.CreateObservableGauge(
            "payment_gateway.reconciliation_last_run_timestamp",
            () => new Measurement<long>(_reconciliationLastRunTimestamp,
                new KeyValuePair<string, object?>("service_name", _serviceName),
                new KeyValuePair<string, object?>("environment", _environment),
                new KeyValuePair<string, object?>("version", _version)),
            description: "Timestamp of last reconciliation run (Unix epoch seconds)");
    }

    public void RecordPaymentTransaction(string providerName, string status, decimal amount, string currency)
    {
        _paymentTransactionsTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordRefundTransaction(string providerName, string status, decimal amount)
    {
        _refundTransactionsTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordWebhookProcessed(string providerName, string eventType, bool success)
    {
        _webhooksProcessedTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("success", success.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordWebhookValidationFailure(string providerName)
    {
        _webhookValidationFailuresTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordWebhookDuration(string providerName, double durationSeconds)
    {
        _webhookDurationSeconds.Record(durationSeconds,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordPaymentDuration(string providerName, double durationSeconds)
    {
        _paymentDurationSeconds.Record(durationSeconds,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void IncrementActiveRequests()
    {
        Interlocked.Increment(ref _activeRequests);
    }

    public void DecrementActiveRequests()
    {
        Interlocked.Decrement(ref _activeRequests);
    }

    public void UpdateProviderHealth(string providerName, double healthScore)
    {
        lock (_providerHealthScores)
        {
            _providerHealthScores[providerName] = healthScore;
        }
    }

    public void RecordReconciliationDiscrepancy(string providerName)
    {
        _reconciliationDiscrepanciesTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void UpdateReconciliationTimestamp()
    {
        Interlocked.Exchange(ref _reconciliationLastRunTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public void RecordReconciliationTransactionsChecked(string providerName, int count)
    {
        _reconciliationTransactionsCheckedTotal.Add(count,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordPaymentStatusQuery(string providerName, double durationSeconds)
    {
        _paymentStatusQueriesTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));

        _paymentStatusQueryDurationSeconds.Record(durationSeconds,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void RecordPaymentStatusCacheHit(string providerName)
    {
        _paymentStatusCacheHitsTotal.Add(1,
            new KeyValuePair<string, object?>("service_name", _serviceName),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("environment", _environment),
            new KeyValuePair<string, object?>("version", _version));
    }

    public void Dispose()
    {
        _meter?.Dispose();
        GC.SuppressFinalize(this);
    }
}
