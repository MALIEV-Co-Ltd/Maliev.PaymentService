using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Core.Entities;
using Maliev.PaymentService.Core.Enums;
using Maliev.PaymentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Controllers;

public class ProvidersControllerTests
{
    private readonly Mock<IProviderManagementService> _serviceMock = new();
    private readonly Mock<ILogger<ProvidersController>> _loggerMock = new();
    private readonly ProvidersController _controller;

    public ProvidersControllerTests()
    {
        _controller = new ProvidersController(_serviceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetActiveByCurrency_MissingCurrency_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetActiveByCurrency("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetProviderById_NotFound_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetProviderByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((PaymentProvider?)null);

        // Act
        var result = await _controller.GetProviderById(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProvider_NotFound_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetProviderByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((PaymentProvider?)null);

        // Act
        var result = await _controller.UpdateProvider(Guid.NewGuid(), new UpdateProviderRequest());

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProviderStatus_KeyNotFound_ReturnsNotFound()
    {
        // Arrange
        _serviceMock.Setup(x => x.UpdateProviderStatusAsync(It.IsAny<Guid>(), It.IsAny<ProviderStatus>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.UpdateProviderStatus(Guid.NewGuid(), new UpdateProviderStatusRequest { Status = ProviderStatus.Disabled });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateProvider_InternalError_Returns500()
    {
        // Arrange
        _serviceMock.Setup(x => x.GetProviderByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ThrowsAsync(new System.Exception("DB Error"));

        // Act
        var result = await _controller.UpdateProvider(Guid.NewGuid(), new UpdateProviderRequest());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteProvider_NotFound_ReturnsNoContent()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteProviderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteProvider(Guid.NewGuid());

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteProvider_InternalError_Returns500()
    {
        // Arrange
        _serviceMock.Setup(x => x.DeleteProviderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Error"));

        // Act
        var result = await _controller.DeleteProvider(Guid.NewGuid());

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}
