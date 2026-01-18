using System.Security.Claims;
using Maliev.PaymentService.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<PermissionAuthorizationHandler>> _loggerMock = new();

    [Fact]
    public async Task HandleRequirementAsync_UserNotAuthenticated_DoesNotSucceed()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.test");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_HasPermissionInClaims_Succeeds()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.test");
        var claims = new[] { new Claim("permissions", "payment.test"), new Claim(ClaimTypes.NameIdentifier, "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ReturnsAsync((byte[]?)null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_HasWildcardPermission_Succeeds()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.any");
        var claims = new[] { new Claim("permissions", "payment.*"), new Claim("sub", "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ReturnsAsync((byte[]?)null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_RevokedCriticalPermission_DoesNotSucceed()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        // payment.payments.process is critical
        var requirement = new PermissionRequirement("payment.payments.process");
        var claims = new[] { new Claim("permissions", "payment.payments.process"), new Claim("sub", "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Mock revocation in cache
        var revocationKey = $"revoked:user:user1:permission:payment.payments.process";
        _cacheMock.Setup(x => x.GetAsync(revocationKey, default)).ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("true"));

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_FallbackToScope_Succeeds()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.test");
        // No "permissions" claim, but has "scope"
        var claims = new[] { new Claim("scope", "payment.test"), new Claim("sub", "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ReturnsAsync((byte[]?)null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_FromCache_Succeeds()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.test");
        var claims = new[] { new Claim("sub", "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        var cachedPermissions = new List<string> { "payment.test" };
        var json = System.Text.Json.JsonSerializer.Serialize(cachedPermissions);
        _cacheMock.Setup(x => x.GetAsync("user:user1:permissions", default)).ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_LacksPermission_DoesNotSucceed()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_cacheMock.Object, _loggerMock.Object);
        var requirement = new PermissionRequirement("payment.needed");
        var claims = new[] { new Claim("permissions", "payment.other"), new Claim("sub", "user1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), default)).ReturnsAsync((byte[]?)null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }
}
