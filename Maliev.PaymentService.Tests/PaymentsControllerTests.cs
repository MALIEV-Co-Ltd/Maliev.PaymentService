using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Common.Enumerations;
using System;

namespace Maliev.PaymentService.Tests
{
    public class PaymentsControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly PaymentsController _controller;

        public PaymentsControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new PaymentsController(_mockService.Object);
        }

        [Fact]
        public async Task GetPayments_ReturnsOkResult_WithListOfPayments()
        {
            // Arrange
            var payments = new List<PaymentDto>
            {
                new PaymentDto { Id = 1, Amount = 100, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 },
                new PaymentDto { Id = 2, Amount = 200, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 }
            };
            _mockService.Setup(s => s.GetPaymentsAsync(PaymentSortType.None)).ReturnsAsync(payments);

            // Act
            var result = await _controller.GetPayments(PaymentSortType.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<PaymentDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPayment_ReturnsOkResult_WhenPaymentExists()
        {
            // Arrange
            var payment = new PaymentDto { Id = 1, Amount = 100, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            _mockService.Setup(s => s.GetPaymentByIdAsync(1)).ReturnsAsync(payment);

            // Act
            var result = await _controller.GetPayment(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetPayment_ReturnsNotFoundResult_WhenPaymentDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetPaymentByIdAsync(99)).ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.GetPayment(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreatePayment_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePaymentRequest { Amount = 300, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            var createdPayment = new PaymentDto { Id = 3, Amount = 300, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            _mockService.Setup(s => s.CreatePaymentAsync(request)).ReturnsAsync(createdPayment);

            // Act
            var result = await _controller.CreatePayment(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetPayment", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdatePayment_ReturnsOkResult_WhenPaymentExists()
        {
            // Arrange
            var request = new UpdatePaymentRequest { Amount = 350, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            var updatedPayment = new PaymentDto { Id = 1, Amount = 350, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            _mockService.Setup(s => s.UpdatePaymentAsync(1, request)).ReturnsAsync(updatedPayment);

            // Act
            var result = await _controller.UpdatePayment(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal(350, returnValue.Amount);
        }

        [Fact]
        public async Task UpdatePayment_ReturnsNotFoundResult_WhenPaymentDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentRequest { Amount = 350, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 };
            _mockService.Setup(s => s.UpdatePaymentAsync(99, request)).ReturnsAsync((PaymentDto?)null);

            // Act
            var result = await _controller.UpdatePayment(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeletePayment_ReturnsNoContentResult_WhenPaymentExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeletePayment(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeletePayment_ReturnsNotFoundResult_WhenPaymentDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeletePayment(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}