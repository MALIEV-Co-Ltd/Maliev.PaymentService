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
    public class PaymentMethodsControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly PaymentMethodsController _controller;

        public PaymentMethodsControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new PaymentMethodsController(_mockService.Object);
        }

        [Fact]
        public async Task GetPaymentMethods_ReturnsOkResult_WithListOfPaymentMethods()
        {
            // Arrange
            var paymentMethods = new List<PaymentMethodDto>
            {
                new PaymentMethodDto { Id = 1, Name = "Cash" },
                new PaymentMethodDto { Id = 2, Name = "Credit Card" }
            };
            _mockService.Setup(s => s.GetPaymentMethodsAsync()).ReturnsAsync(paymentMethods);

            // Act
            var result = await _controller.GetPaymentMethods();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<PaymentMethodDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPaymentMethod_ReturnsOkResult_WhenPaymentMethodExists()
        {
            // Arrange
            var paymentMethod = new PaymentMethodDto { Id = 1, Name = "Cash" };
            _mockService.Setup(s => s.GetPaymentMethodByIdAsync(1)).ReturnsAsync(paymentMethod);

            // Act
            var result = await _controller.GetPaymentMethod(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentMethodDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetPaymentMethod_ReturnsNotFoundResult_WhenPaymentMethodDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetPaymentMethodByIdAsync(99)).ReturnsAsync((PaymentMethodDto?)null);

            // Act
            var result = await _controller.GetPaymentMethod(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreatePaymentMethod_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePaymentMethodRequest { Name = "New Method" };
            var createdPaymentMethod = new PaymentMethodDto { Id = 3, Name = "New Method" };
            _mockService.Setup(s => s.CreatePaymentMethodAsync(request)).ReturnsAsync(createdPaymentMethod);

            // Act
            var result = await _controller.CreatePaymentMethod(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentMethodDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetPaymentMethod", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdatePaymentMethod_ReturnsOkResult_WhenPaymentMethodExists()
        {
            // Arrange
            var request = new UpdatePaymentMethodRequest { Name = "Updated Method" };
            var updatedPaymentMethod = new PaymentMethodDto { Id = 1, Name = "Updated Method" };
            _mockService.Setup(s => s.UpdatePaymentMethodAsync(1, request)).ReturnsAsync(updatedPaymentMethod);

            // Act
            var result = await _controller.UpdatePaymentMethod(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentMethodDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Updated Method", returnValue.Name);
        }

        [Fact]
        public async Task UpdatePaymentMethod_ReturnsNotFoundResult_WhenPaymentMethodDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentMethodRequest { Name = "Updated Method" };
            _mockService.Setup(s => s.UpdatePaymentMethodAsync(99, request)).ReturnsAsync((PaymentMethodDto?)null);

            // Act
            var result = await _controller.UpdatePaymentMethod(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeletePaymentMethod_ReturnsNoContentResult_WhenPaymentMethodExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentMethodAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeletePaymentMethod(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeletePaymentMethod_ReturnsNotFoundResult_WhenPaymentMethodDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentMethodAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeletePaymentMethod(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}