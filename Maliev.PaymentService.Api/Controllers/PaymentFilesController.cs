using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/paymentfiles")]
    public class PaymentFilesController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public PaymentFilesController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentFileDto>>> GetPaymentFiles()
        {
            var paymentFiles = await _paymentServiceService.GetPaymentFilesAsync();
            return Ok(paymentFiles);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentFileDto>> GetPaymentFile(int id)
        {
            var paymentFile = await _paymentServiceService.GetPaymentFileByIdAsync(id);
            if (paymentFile == null)
            {
                return NotFound();
            }
            return Ok(paymentFile);
        }

        [HttpPost]
        public async Task<ActionResult<PaymentFileDto>> CreatePaymentFile(CreatePaymentFileRequest request)
        {
            var paymentFile = await _paymentServiceService.CreatePaymentFileAsync(request);
            return CreatedAtAction(nameof(GetPaymentFile), new { id = paymentFile.Id }, paymentFile);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PaymentFileDto>> UpdatePaymentFile(int id, UpdatePaymentFileRequest request)
        {
            var paymentFile = await _paymentServiceService.UpdatePaymentFileAsync(id, request);
            if (paymentFile == null)
            {
                return NotFound();
            }
            return Ok(paymentFile);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaymentFile(int id)
        {
            var result = await _paymentServiceService.DeletePaymentFileAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}