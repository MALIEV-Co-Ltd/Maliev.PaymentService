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
    public class PaymentTypesControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly PaymentTypesController _controller;

        public PaymentTypesControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new PaymentTypesController(_mockService.Object);
        }

        [Fact]
        public async Task GetPaymentTypes_ReturnsOkResult_WithListOfPaymentTypes()
        {
            // Arrange
            var paymentTypes = new List<PaymentTypeDto>
            {
                new PaymentTypeDto { Id = 1, Name = "Salary" },
                new PaymentTypeDto { Id = 2, Name = "Groceries" }
            };
            _mockService.Setup(s => s.GetPaymentTypesAsync()).ReturnsAsync(paymentTypes);

            // Act
            var result = await _controller.GetPaymentTypes();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<PaymentTypeDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPaymentType_ReturnsOkResult_WhenPaymentTypeExists()
        {
            // Arrange
            var paymentType = new PaymentTypeDto { Id = 1, Name = "Salary" };
            _mockService.Setup(s => s.GetPaymentTypeByIdAsync(1)).ReturnsAsync(paymentType);

            // Act
            var result = await _controller.GetPaymentType(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentTypeDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetPaymentType_ReturnsNotFoundResult_WhenPaymentTypeDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetPaymentTypeByIdAsync(99)).ReturnsAsync((PaymentTypeDto?)null);

            // Act
            var result = await _controller.GetPaymentType(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreatePaymentType_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePaymentTypeRequest { Name = "New Type" };
            var createdPaymentType = new PaymentTypeDto { Id = 3, Name = "New Type" };
            _mockService.Setup(s => s.CreatePaymentTypeAsync(request)).ReturnsAsync(createdPaymentType);

            // Act
            var result = await _controller.CreatePaymentType(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentTypeDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetPaymentType", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdatePaymentType_ReturnsOkResult_WhenPaymentTypeExists()
        {
            // Arrange
            var request = new UpdatePaymentTypeRequest { Name = "Updated Type" };
            var updatedPaymentType = new PaymentTypeDto { Id = 1, Name = "Updated Type" };
            _mockService.Setup(s => s.UpdatePaymentTypeAsync(1, request)).ReturnsAsync(updatedPaymentType);

            // Act
            var result = await _controller.UpdatePaymentType(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentTypeDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("Updated Type", returnValue.Name);
        }

        [Fact]
        public async Task UpdatePaymentType_ReturnsNotFoundResult_WhenPaymentTypeDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentTypeRequest { Name = "Updated Type" };
            _mockService.Setup(s => s.UpdatePaymentTypeAsync(99, request)).ReturnsAsync((PaymentTypeDto?)null);

            // Act
            var result = await _controller.UpdatePaymentType(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeletePaymentType_ReturnsNoContentResult_WhenPaymentTypeExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentTypeAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeletePaymentType(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeletePaymentType_ReturnsNotFoundResult_WhenPaymentTypeDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentTypeAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeletePaymentType(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}