using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/summary")]
    public class SummaryController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public SummaryController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet("financial")]
        public async Task<ActionResult<FinancialSummaryDto>> GetFinancialSummary()
        {
            var summary = await _paymentServiceService.GetFinancialSummaryAsync();
            return Ok(summary);
        }

        [HttpGet("details")]
        public async Task<ActionResult<IEnumerable<SummaryDetailDto>>> GetSummaryDetails()
        {
            var details = await _paymentServiceService.GetSummaryDetailsAsync();
            return Ok(details);
        }
    }
}