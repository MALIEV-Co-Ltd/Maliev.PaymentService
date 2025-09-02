using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/paymenttypes")]
    public class PaymentTypesController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public PaymentTypesController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentTypeDto>>> GetPaymentTypes()
        {
            var paymentTypes = await _paymentServiceService.GetPaymentTypesAsync();
            return Ok(paymentTypes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentTypeDto>> GetPaymentType(int id)
        {
            var paymentType = await _paymentServiceService.GetPaymentTypeByIdAsync(id);
            if (paymentType == null)
            {
                return NotFound();
            }
            return Ok(paymentType);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentTypeDto>> CreatePaymentType(CreatePaymentTypeRequest request)
        {
            var paymentType = await _paymentServiceService.CreatePaymentTypeAsync(request);
            return CreatedAtAction(nameof(GetPaymentType), new { id = paymentType.Id }, paymentType);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentTypeDto>> UpdatePaymentType(int id, UpdatePaymentTypeRequest request)
        {
            var paymentType = await _paymentServiceService.UpdatePaymentTypeAsync(id, request);
            if (paymentType == null)
            {
                return NotFound();
            }
            return Ok(paymentType);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentType(int id)
        {
            var result = await _paymentServiceService.DeletePaymentTypeAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}