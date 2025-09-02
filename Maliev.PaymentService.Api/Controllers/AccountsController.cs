using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IPaymentServiceService _paymentServiceService;

        public AccountsController(IPaymentServiceService paymentServiceService)
        {
            _paymentServiceService = paymentServiceService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> GetAccounts()
        {
            var accounts = await _paymentServiceService.GetAccountsAsync();
            return Ok(accounts);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AccountDto>> GetAccount(int id)
        {
            var account = await _paymentServiceService.GetAccountByIdAsync(id);
            if (account == null)
            {
                return NotFound();
            }
            return Ok(account);
        }

        [HttpPost]
        public async Task<ActionResult<AccountDto>> CreateAccount(CreateAccountRequest request)
        {
            var account = await _paymentServiceService.CreateAccountAsync(request);
            return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AccountDto>> UpdateAccount(int id, UpdateAccountRequest request)
        {
            var account = await _paymentServiceService.UpdateAccountAsync(id, request);
            if (account == null)
            {
                return NotFound();
            }
            return Ok(account);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var result = await _paymentServiceService.DeleteAccountAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}