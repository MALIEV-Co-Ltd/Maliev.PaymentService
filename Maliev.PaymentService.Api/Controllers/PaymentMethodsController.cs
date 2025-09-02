using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/paymentmethods")]
    public class PaymentMethodsController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public PaymentMethodsController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentMethodDto>>> GetPaymentMethods()
        {
            var paymentMethods = await _paymentServiceService.GetPaymentMethodsAsync();
            return Ok(paymentMethods);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentMethodDto>> GetPaymentMethod(int id)
        {
            var paymentMethod = await _paymentServiceService.GetPaymentMethodByIdAsync(id);
            if (paymentMethod == null)
            {
                return NotFound();
            }
            return Ok(paymentMethod);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentMethodDto>> CreatePaymentMethod(CreatePaymentMethodRequest request)
        {
            var paymentMethod = await _paymentServiceService.CreatePaymentMethodAsync(request);
            return CreatedAtAction(nameof(GetPaymentMethod), new { id = paymentMethod.Id }, paymentMethod);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentMethodDto>> UpdatePaymentMethod(int id, UpdatePaymentMethodRequest request)
        {
            var paymentMethod = await _paymentServiceService.UpdatePaymentMethodAsync(id, request);
            if (paymentMethod == null)
            {
                return NotFound();
            }
            return Ok(paymentMethod);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentMethod(int id)
        {
            var result = await _paymentServiceService.DeletePaymentMethodAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}