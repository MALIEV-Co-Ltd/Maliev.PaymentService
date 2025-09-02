using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/paymentdirections")]
    public class PaymentDirectionsController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public PaymentDirectionsController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentDirectionDto>>> GetPaymentDirections()
        {
            var paymentDirections = await _paymentServiceService.GetPaymentDirectionsAsync();
            return Ok(paymentDirections);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDirectionDto>> GetPaymentDirection(int id)
        {
            var paymentDirection = await _paymentServiceService.GetPaymentDirectionByIdAsync(id);
            if (paymentDirection == null)
            {
                return NotFound();
            }
            return Ok(paymentDirection);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentDirectionDto>> CreatePaymentDirection(CreatePaymentDirectionRequest request)
        {
            var paymentDirection = await _paymentServiceService.CreatePaymentDirectionAsync(request);
            return CreatedAtAction(nameof(GetPaymentDirection), new { id = paymentDirection.Id }, paymentDirection);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentDirectionDto>> UpdatePaymentDirection(int id, UpdatePaymentDirectionRequest request)
        {
            var paymentDirection = await _paymentServiceService.UpdatePaymentDirectionAsync(id, request);
            if (paymentDirection == null)
            {
                return NotFound();
            }
            return Ok(paymentDirection);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentDirection(int id)
        {
            var result = await _paymentServiceService.DeletePaymentDirectionAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}