using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maliev.PaymentService.Api.Authorization;
using Maliev.PaymentService.Tests.Fixtures;
using Xunit;

namespace Maliev.PaymentService.Tests.Integration.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public class RoleAccessTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public RoleAccessTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData(PaymentPredefinedRoles.Viewer, PaymentPermissions.PaymentsRead, true)]
    [InlineData(PaymentPredefinedRoles.Viewer, PaymentPermissions.PaymentsProcess, false)]
    [InlineData(PaymentPredefinedRoles.Processor, PaymentPermissions.PaymentsProcess, true)]
    [InlineData(PaymentPredefinedRoles.Accountant, PaymentPermissions.PaymentsReconcile, true)]
    public async Task Role_ShouldHave_ExpectedAccess(string role, string permission, bool shouldHaveAccess)
    {
        // Arrange
        // Use a unique User ID per role to avoid cache collisions in tests
        var userId = $"test-user-{role}";
        var permissions = PaymentPredefinedRoles.GetPermissions(role).ToArray();
        var token = _factory.CreateTestJwtToken(userId: userId, permissions: permissions);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        // We need a dummy endpoint or check against a real one that requires the 'permission'
        // For this test, we'll check against PaymentsController endpoints which we already secured
        string path = permission switch
        {
            PaymentPermissions.PaymentsRead => "payment/v1/payments/" + Guid.NewGuid(),
            PaymentPermissions.PaymentsProcess => "payment/v1/payments",
            _ => "payment/v1/payments" // Default
        };

        HttpResponseMessage response;
        if (permission == PaymentPermissions.PaymentsProcess)
        {
            var request = new
            {
                Amount = 100.00m,
                Currency = "THB",
                CustomerId = "cust_123",
                OrderId = "order_123",
                Description = "Test payment",
                ReturnUrl = "https://example.com/return",
                CancelUrl = "https://example.com/cancel"
            };
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            response = await client.PostAsJsonAsync(path, request);
        }
        else
        {
            response = await client.GetAsync(path);
        }

        // Assert
        if (shouldHaveAccess)
        {
            // Should not be Forbidden (might be 400 or 404 depending on the dummy data)
            Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
