using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Common.Enumerations;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public PaymentsController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPayments([FromQuery] PaymentSortType sortType = PaymentSortType.None)
        {
            var payments = await _paymentServiceService.GetPaymentsAsync(sortType);
            return Ok(payments);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDto>> GetPayment(int id)
        {
            var payment = await _paymentServiceService.GetPaymentByIdAsync(id);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentDto>> CreatePayment(CreatePaymentRequest request)
        {
            var payment = await _paymentServiceService.CreatePaymentAsync(request);
            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentDto>> UpdatePayment(int id, UpdatePaymentRequest request)
        {
            var payment = await _paymentServiceService.UpdatePaymentAsync(id, request);
            if (payment == null)
            {
                return NotFound();
            }
            return Ok(payment);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var result = await _paymentServiceService.DeletePaymentAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}