using Maliev.PaymentService.Application.Interfaces;
using Maliev.PaymentService.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class InMemoryIdempotencyServiceTests
{
    private readonly Mock<ILogger<InMemoryIdempotencyService>> _loggerMock;
    private readonly InMemoryIdempotencyService _service;

    public InMemoryIdempotencyServiceTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryIdempotencyService>>();
        _service = new InMemoryIdempotencyService(_loggerMock.Object);
    }

    [Fact]
    public async Task IsProcessedAsync_NotExists_ShouldReturnFalse()
    {
        var result = await _service.IsProcessedAsync("payment", "key-123");

        Assert.False(result);
    }

    [Fact]
    public async Task IsProcessedAsync_Exists_ShouldReturnTrue()
    {
        await _service.StoreResultAsync("payment", "key-123", "result");

        var result = await _service.IsProcessedAsync("payment", "key-123");

        Assert.True(result);
    }

    [Fact]
    public async Task StoreResultAsync_ShouldStoreResult()
    {
        await _service.StoreResultAsync("payment", "key-123", "result-data");

        var result = await _service.GetResultAsync("payment", "key-123");

        Assert.Equal("result-data", result);
    }

    [Fact]
    public async Task GetResultAsync_NotExists_ShouldReturnNull()
    {
        var result = await _service.GetResultAsync("payment", "nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetResultAsync_DifferentOperationType_ShouldReturnNull()
    {
        await _service.StoreResultAsync("payment", "key-123", "result");

        var result = await _service.GetResultAsync("refund", "key-123");

        Assert.Null(result);
    }

    [Fact]
    public async Task AcquireLockAsync_FirstCall_ShouldReturnTrue()
    {
        var result = await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));

        Assert.True(result);
    }

    [Fact]
    public async Task AcquireLockAsync_AlreadyAcquired_ShouldReturnFalse()
    {
        await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));

        var result = await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));

        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseLockAsync_ShouldReleaseLock()
    {
        await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));

        await _service.ReleaseLockAsync("payment", "key-123");

        var result = await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));

        Assert.True(result);
    }

    [Fact]
    public async Task ReleaseLockAsync_NotAcquired_ShouldNotThrow()
    {
        await _service.ReleaseLockAsync("payment", "key-123");
    }

    [Fact]
    public async Task AcquireLockAsync_DifferentKeys_ShouldWork()
    {
        var result1 = await _service.AcquireLockAsync("payment", "key-1", TimeSpan.FromSeconds(10));
        var result2 = await _service.AcquireLockAsync("payment", "key-2", TimeSpan.FromSeconds(10));

        Assert.True(result1);
        Assert.True(result2);
    }

    [Fact]
    public async Task AcquireLockAsync_DifferentOperations_ShouldWork()
    {
        var result1 = await _service.AcquireLockAsync("payment", "key-123", TimeSpan.FromSeconds(10));
        var result2 = await _service.AcquireLockAsync("refund", "key-123", TimeSpan.FromSeconds(10));

        Assert.True(result1);
        Assert.True(result2);
    }
}
