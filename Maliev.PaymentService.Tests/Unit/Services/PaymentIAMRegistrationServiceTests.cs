using Maliev.PaymentService.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Services;

public class PaymentIAMRegistrationServiceTests
{
    [Fact]
    public void GetPermissions_ReturnsAllPermissions()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        var loggerMock = new Mock<ILogger<PaymentIAMRegistrationService>>();
        var service = new TestPaymentIAMRegistrationService(configMock.Object, loggerMock.Object);

        // Act
        var permissions = service.ExposeGetPermissions();

        // Assert
        Assert.NotEmpty(permissions);
        Assert.Contains(permissions, p => p.PermissionId == "payment.payments.process");
    }

    [Fact]
    public void GetPredefinedRoles_ReturnsAllRoles()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        var loggerMock = new Mock<ILogger<PaymentIAMRegistrationService>>();
        var service = new TestPaymentIAMRegistrationService(configMock.Object, loggerMock.Object);

        // Act
        var roles = service.ExposeGetPredefinedRoles();

        // Assert
        Assert.NotEmpty(roles);
        Assert.Contains(roles, r => r.RoleId == "roles.payment.admin");
    }

    private class TestPaymentIAMRegistrationService : PaymentIAMRegistrationService
    {
        public TestPaymentIAMRegistrationService(IConfiguration configuration, ILogger<PaymentIAMRegistrationService> logger) 
            : base(configuration, logger) { }

        public IEnumerable<Maliev.Aspire.ServiceDefaults.IAM.PermissionRegistration> ExposeGetPermissions() => GetPermissions();
        public IEnumerable<Maliev.Aspire.ServiceDefaults.IAM.RoleRegistration> ExposeGetPredefinedRoles() => GetPredefinedRoles();
    }
}
