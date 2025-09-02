using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Api.Models;
using System;

namespace Maliev.PaymentService.Tests
{
    public class PaymentFilesControllerTests
    {
        private readonly Mock<IPaymentServiceService> _mockService;
        private readonly PaymentFilesController _controller;

        public PaymentFilesControllerTests()
        {
            _mockService = new Mock<IPaymentServiceService>();
            _controller = new PaymentFilesController(_mockService.Object);
        }

        [Fact]
        public async Task GetPaymentFiles_ReturnsOkResult_WithListOfPaymentFiles()
        {
            // Arrange
            var paymentFiles = new List<PaymentFileDto>
            {
                new PaymentFileDto { Id = 1, FileName = "File1.pdf", FilePath = "/path/file1.pdf", UploadDate = DateTime.Now, PaymentId = 1 },
                new PaymentFileDto { Id = 2, FileName = "File2.jpg", FilePath = "/path/file2.jpg", UploadDate = DateTime.Now, PaymentId = 1 }
            };
            _mockService.Setup(s => s.GetPaymentFilesAsync()).ReturnsAsync(paymentFiles);

            // Act
            var result = await _controller.GetPaymentFiles();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<PaymentFileDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }

        [Fact]
        public async Task GetPaymentFile_ReturnsOkResult_WhenPaymentFileExists()
        {
            // Arrange
            var paymentFile = new PaymentFileDto { Id = 1, FileName = "File1.pdf", FilePath = "/path/file1.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            _mockService.Setup(s => s.GetPaymentFileByIdAsync(1)).ReturnsAsync(paymentFile);

            // Act
            var result = await _controller.GetPaymentFile(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentFileDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
        }

        [Fact]
        public async Task GetPaymentFile_ReturnsNotFoundResult_WhenPaymentFileDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.GetPaymentFileByIdAsync(99)).ReturnsAsync((PaymentFileDto?)null);

            // Act
            var result = await _controller.GetPaymentFile(99);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreatePaymentFile_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new CreatePaymentFileRequest { FileName = "NewFile.pdf", FilePath = "/new/path/newfile.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            var createdPaymentFile = new PaymentFileDto { Id = 3, FileName = "NewFile.pdf", FilePath = "/new/path/newfile.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            _mockService.Setup(s => s.CreatePaymentFileAsync(request)).ReturnsAsync(createdPaymentFile);

            // Act
            var result = await _controller.CreatePaymentFile(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PaymentFileDto>(createdAtActionResult.Value);
            Assert.Equal(3, returnValue.Id);
            Assert.Equal("GetPaymentFile", createdAtActionResult.ActionName);
        }

        [Fact]
        public async Task UpdatePaymentFile_ReturnsOkResult_WhenPaymentFileExists()
        {
            // Arrange
            var request = new UpdatePaymentFileRequest { FileName = "UpdatedFile.pdf", FilePath = "/path/updatedfile.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            var updatedPaymentFile = new PaymentFileDto { Id = 1, FileName = "UpdatedFile.pdf", FilePath = "/path/updatedfile.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            _mockService.Setup(s => s.UpdatePaymentFileAsync(1, request)).ReturnsAsync(updatedPaymentFile);

            // Act
            var result = await _controller.UpdatePaymentFile(1, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PaymentFileDto>(okResult.Value);
            Assert.Equal(1, returnValue.Id);
            Assert.Equal("UpdatedFile.pdf", returnValue.FileName);
        }

        [Fact]
        public async Task UpdatePaymentFile_ReturnsNotFoundResult_WhenPaymentFileDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentFileRequest { FileName = "UpdatedFile.pdf", FilePath = "/path/updatedfile.pdf", UploadDate = DateTime.Now, PaymentId = 1 };
            _mockService.Setup(s => s.UpdatePaymentFileAsync(99, request)).ReturnsAsync((PaymentFileDto?)null);

            // Act
            var result = await _controller.UpdatePaymentFile(99, request);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task DeletePaymentFile_ReturnsNoContentResult_WhenPaymentFileExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentFileAsync(1)).ReturnsAsync(true);

            // Act
            var result = await _controller.DeletePaymentFile(1);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeletePaymentFile_ReturnsNotFoundResult_WhenPaymentFileDoesNotExist()
        {
            // Arrange
            _mockService.Setup(s => s.DeletePaymentFileAsync(99)).ReturnsAsync(false);

            // Act
            var result = await _controller.DeletePaymentFile(99);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}