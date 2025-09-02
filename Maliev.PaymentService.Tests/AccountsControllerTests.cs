using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Api.Models;

namespace Maliev.PaymentService.Tests
{
    public class AccountsControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly AccountsController _controller;

        public AccountsControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new AccountsController(_mockService.Object);
        }

        [Fact]
        public async Task GetAccounts_ReturnsOkResult_WithListOfAccounts()
        {
            // Arrange
            var accounts = new List<AccountDto>
            {
                new AccountDto { Id = 1, Name = "Account1", Balance = 100 },
                new AccountDto { Id = 2, Name = "Account2", Balance = 200 }
            };
            _mockService.Setup(s => s.GetAccountsAsync()).ReturnsAsync(accounts);

            // Act
            var result = await _controller.GetAccounts();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<AccountDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetAccount_ReturnsOkResult_WhenAccountExists()
        {
            // Arrange
            var account = new AccountDto { Id = 1, Name = "Account1", Balance = 100 };
            _mockService.Setup(s => s.GetAccountByIdAsync(1)).ReturnsAsync(account);

            // Act
            var result = await _controller.GetAccount(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<AccountDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetAccount_ReturnsNotFoundResult_WhenAccountDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetAccountByIdAsync(99)).ReturnsAsync((AccountDto?)null);

            // Act
            var result = await _controller.GetAccount(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateAccount_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreateAccountRequest { Name = "New Account", Balance = 500 };
            var createdAccount = new AccountDto { Id = 3, Name = "New Account", Balance = 500 };
            _mockService.Setup(s => s.CreateAccountAsync(request)).ReturnsAsync(createdAccount);

            // Act
            var result = await _controller.CreateAccount(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<AccountDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetAccount", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsOkResult_WhenAccountExists()
        {
            // Arrange
            var request = new UpdateAccountRequest { Name = "Updated Account", Balance = 150 };
            var updatedAccount = new AccountDto { Id = 1, Name = "Updated Account", Balance = 150 };
            _mockService.Setup(s => s.UpdateAccountAsync(1, request)).ReturnsAsync(updatedAccount);

            // Act
            var result = await _controller.UpdateAccount(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<AccountDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Updated Account", returnValue.Name);
        }

        [Fact]
        public async Task UpdateAccount_ReturnsNotFoundResult_WhenAccountDoesNotExist()
        {
            // Arrange
            var request = new UpdateAccountRequest { Name = "Updated Account", Balance = 150 };
            _mockService.Setup(s => s.UpdateAccountAsync(99, request)).ReturnsAsync((AccountDto?)null);

            // Act
            var result = await _controller.UpdateAccount(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeleteAccount_ReturnsNoContentResult_WhenAccountExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeleteAccountAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteAccount(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteAccount_ReturnsNotFoundResult_WhenAccountDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeleteAccountAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteAccount(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}