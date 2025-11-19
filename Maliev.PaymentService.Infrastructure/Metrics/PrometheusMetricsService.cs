using Maliev.PaymentService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.PaymentService.Infrastructure.Metrics;

/// <summary>
/// Prometheus implementation of IMetricsService for metrics collection and reporting.
/// </summary>
public class PrometheusMetricsService : IMetricsService
{
    private readonly string _serviceName;
    private readonly string _environment;
    private readonly string _version;

    public PrometheusMetricsService(IConfiguration configuration)
    {
        _serviceName = "payment-gateway-service";
        _environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        _version = configuration["ServiceVersion"] ?? "1.0.0";
    }

    public void RecordPaymentTransaction(string providerName, string status, decimal amount, string currency)
    {
        MetricDefinitions.PaymentTransactionsTotal
            .WithLabels(_serviceName, providerName, status, currency, _environment, _version)
            .Inc();
    }

    public void RecordRefundTransaction(string providerName, string status, decimal amount)
    {
        MetricDefinitions.RefundTransactionsTotal
            .WithLabels(_serviceName, providerName, status, _environment, _version)
            .Inc();
    }

    public void RecordWebhookProcessed(string providerName, string eventType, bool success)
    {
        MetricDefinitions.WebhooksProcessedTotal
            .WithLabels(_serviceName, providerName, eventType, success.ToString().ToLowerInvariant(), _environment, _version)
            .Inc();
    }

    public void RecordWebhookValidationFailure(string providerName)
    {
        MetricDefinitions.WebhookValidationFailuresTotal
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Inc();
    }

    public void RecordWebhookDuration(string providerName, double durationSeconds)
    {
        MetricDefinitions.WebhookDurationSeconds
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Observe(durationSeconds);
    }

    public void RecordPaymentDuration(string providerName, double durationSeconds)
    {
        MetricDefinitions.PaymentDurationSeconds
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Observe(durationSeconds);
    }

    public void IncrementActiveRequests()
    {
        MetricDefinitions.ActiveRequestsGauge
            .WithLabels(_serviceName, _environment, _version)
            .Inc();
    }

    public void DecrementActiveRequests()
    {
        MetricDefinitions.ActiveRequestsGauge
            .WithLabels(_serviceName, _environment, _version)
            .Dec();
    }

    public void UpdateProviderHealth(string providerName, double healthScore)
    {
        MetricDefinitions.ProviderHealthGauge
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Set(healthScore);
    }

    public void RecordReconciliationDiscrepancy(string providerName)
    {
        MetricDefinitions.ReconciliationDiscrepanciesTotal
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Inc();
    }

    public void UpdateReconciliationTimestamp()
    {
        MetricDefinitions.ReconciliationLastRunTimestamp
            .WithLabels(_serviceName, _environment, _version)
            .Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public void RecordReconciliationTransactionsChecked(string providerName, int count)
    {
        MetricDefinitions.ReconciliationTransactionsCheckedTotal
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Inc(count);
    }

    public void RecordPaymentStatusQuery(string providerName, double durationSeconds)
    {
        MetricDefinitions.PaymentStatusQueriesTotal
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Inc();

        MetricDefinitions.PaymentStatusQueryDurationSeconds
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Observe(durationSeconds);
    }

    public void RecordPaymentStatusCacheHit(string providerName)
    {
        MetricDefinitions.PaymentStatusCacheHitsTotal
            .WithLabels(_serviceName, providerName, _environment, _version)
            .Inc();
    }
}
