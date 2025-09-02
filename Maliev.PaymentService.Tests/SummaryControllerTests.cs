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
    public class SummaryControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly SummaryController _controller;

        public SummaryControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new SummaryController(_mockService.Object);
        }

        [Fact]
        public async Task GetFinancialSummary_ReturnsOkResult_WithFinancialSummaryDto()
        {
            // Arrange
            var financialSummary = new FinancialSummaryDto { TotalIncome = 1000, TotalExpense = 500, NetBalance = 500 };
            _mockService.Setup(s => s.GetFinancialSummaryAsync()).ReturnsAsync(financialSummary);

            // Act
            var result = await _controller.GetFinancialSummary();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<FinancialSummaryDto>(okResult.Value);
            Assert.Equal(1000, returnValue.TotalIncome);
        }

        [Fact]
        public async Task GetSummaryDetails_ReturnsOkResult_WithListOfSummaryDetailDto()
        {
            // Arrange
            var summaryDetails = new List<SummaryDetailDto>
            {
                new SummaryDetailDto { Category = "Salary", Amount = 1000 },
                new SummaryDetailDto { Category = "Groceries", Amount = 300 }
            };
            _mockService.Setup(s => s.GetSummaryDetailsAsync()).ReturnsAsync(summaryDetails);

            // Act
            var result = await _controller.GetSummaryDetails();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<SummaryDetailDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }
    }
}