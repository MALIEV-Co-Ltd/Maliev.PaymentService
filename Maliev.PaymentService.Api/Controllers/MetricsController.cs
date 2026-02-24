using Asp.Versioning;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PaymentService.Api.Controllers;

/// <summary>
/// Lightweight business metrics for dashboards.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("payment/v{version:apiVersion}/metrics")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ILogger<MetricsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MetricsController"/>.
    /// </summary>
    public MetricsController(IPaymentRepository paymentRepository, ILogger<MetricsController> logger)
    {
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get payment statistics for the dashboard.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PaymentStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var tomorrow = today.AddDays(1);
        var yesterday = today.AddDays(-1);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var todayPayments = await _paymentRepository.GetByDateRangeAsync(today, tomorrow, cancellationToken);
        var yesterdayPayments = await _paymentRepository.GetByDateRangeAsync(yesterday, today, cancellationToken);
        var monthPayments = await _paymentRepository.GetByDateRangeAsync(monthStart, tomorrow, cancellationToken);

        var todayTotal = todayPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var yesterdayTotal = yesterdayPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        double percentageChange = 0;
        if (yesterdayTotal > 0)
        {
            percentageChange = (double)((todayTotal - yesterdayTotal) / yesterdayTotal * 100);
        }

        var stats = new PaymentStatsDto
        {
            TodayTotal = todayTotal,
            PercentageChangeFromYesterday = percentageChange,
            PendingTotal = todayPayments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            PendingCount = todayPayments.Count(p => p.Status == PaymentStatus.Pending),
            MonthTotal = monthPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            FailedTotal = todayPayments.Where(p => p.Status == PaymentStatus.Failed).Sum(p => p.Amount),
            FailedCount = todayPayments.Count(p => p.Status == PaymentStatus.Failed)
        };

        return Ok(stats);
    }
}

/// <summary>
/// Statistics for payments.
/// </summary>
public class PaymentStatsDto
{
    /// <summary>Total completed payment amount today.</summary>
    public decimal TodayTotal { get; set; }

    /// <summary>Percentage change in completed payments vs yesterday.</summary>
    public double PercentageChangeFromYesterday { get; set; }

    /// <summary>Total amount of pending payments today.</summary>
    public decimal PendingTotal { get; set; }

    /// <summary>Count of pending payments today.</summary>
    public int PendingCount { get; set; }

    /// <summary>Total completed payment amount this month.</summary>
    public decimal MonthTotal { get; set; }

    /// <summary>Total amount of failed payments today.</summary>
    public decimal FailedTotal { get; set; }

    /// <summary>Count of failed payments today.</summary>
    public int FailedCount { get; set; }
}
