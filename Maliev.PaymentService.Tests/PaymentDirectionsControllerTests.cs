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
    public class PaymentDirectionsControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly PaymentDirectionsController _controller;

        public PaymentDirectionsControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new PaymentDirectionsController(_mockService.Object);
        }

        [Fact]
        public async Task GetPaymentDirections_ReturnsOkResult_WithListOfPaymentDirections()
        {
            // Arrange
            var paymentDirections = new List<PaymentDirectionDto>
            {
                new PaymentDirectionDto { Id = 1, Name = "Income" },
                new PaymentDirectionDto { Id = 2, Name = "Expense" }
            };
            _mockService.Setup(s => s.GetPaymentDirectionsAsync()).ReturnsAsync(paymentDirections);

            // Act
            var result = await _controller.GetPaymentDirections();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<PaymentDirectionDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPaymentDirection_ReturnsOkResult_WhenPaymentDirectionExists()
        {
            // Arrange
            var paymentDirection = new PaymentDirectionDto { Id = 1, Name = "Income" };
            _mockService.Setup(s => s.GetPaymentDirectionByIdAsync(1)).ReturnsAsync(paymentDirection);

            // Act
            var result = await _controller.GetPaymentDirection(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDirectionDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetPaymentDirection_ReturnsNotFoundResult_WhenPaymentDirectionDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetPaymentDirectionByIdAsync(99)).ReturnsAsync((PaymentDirectionDto?)null);

            // Act
            var result = await _controller.GetPaymentDirection(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreatePaymentDirection_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePaymentDirectionRequest { Name = "New Direction" };
            var createdPaymentDirection = new PaymentDirectionDto { Id = 3, Name = "New Direction" };
            _mockService.Setup(s => s.CreatePaymentDirectionAsync(request)).ReturnsAsync(createdPaymentDirection);

            // Act
            var result = await _controller.CreatePaymentDirection(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDirectionDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetPaymentDirection", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdatePaymentDirection_ReturnsOkResult_WhenPaymentDirectionExists()
        {
            // Arrange
            var request = new UpdatePaymentDirectionRequest { Name = "Updated Direction" };
            var updatedPaymentDirection = new PaymentDirectionDto { Id = 1, Name = "Updated Direction" };
            _mockService.Setup(s => s.UpdatePaymentDirectionAsync(1, request)).ReturnsAsync(updatedPaymentDirection);

            // Act
            var result = await _controller.UpdatePaymentDirection(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentDirectionDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Updated Direction", returnValue.Name);
        }

        [Fact]
        public async Task UpdatePaymentDirection_ReturnsNotFoundResult_WhenPaymentDirectionDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentDirectionRequest { Name = "Updated Direction" };
            _mockService.Setup(s => s.UpdatePaymentDirectionAsync(99, request)).ReturnsAsync((PaymentDirectionDto?)null);

            // Act
            var result = await _controller.UpdatePaymentDirection(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeletePaymentDirection_ReturnsNoContentResult_WhenPaymentDirectionExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentDirectionAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeletePaymentDirection(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeletePaymentDirection_ReturnsNotFoundResult_WhenPaymentDirectionDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentDirectionAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeletePaymentDirection(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}