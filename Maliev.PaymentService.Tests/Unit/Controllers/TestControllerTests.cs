using System.Text.Json;
using Maliev.MessagingContracts.Contracts.Payments;
using Maliev.PaymentService.Api.Controllers;
using Maliev.PaymentService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.PaymentService.Tests.Unit.Controllers;

public sealed class TestControllerTests
{
    [Fact]
    public async Task PublishPaymentCompleted_ProviderIsSupplied_SerializesProviderName()
    {
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        try
        {
            PaymentCompletedEvent? publishedEvent = null;
            var eventPublisher = new Mock<IEventPublisher>();
            eventPublisher
                .Setup(publisher => publisher.PublishAsync(
                    It.IsAny<PaymentCompletedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<PaymentCompletedEvent, CancellationToken>(
                    (paymentEvent, _) => publishedEvent = paymentEvent)
                .Returns(Task.CompletedTask);

            var controller = new TestController(
                eventPublisher.Object,
                Mock.Of<ILogger<TestController>>());
            var request = new PublishPaymentRequest
            {
                OrderId = Guid.NewGuid(),
                CustomerId = "customer-123",
                PaymentId = Guid.NewGuid(),
                Amount = 1250,
                Currency = "THB",
                ProviderName = "omise"
            };

            var result = await controller.PublishPaymentCompleted(request);

            Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(publishedEvent);

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(publishedEvent));
            Assert.Equal(
                "omise",
                document.RootElement.GetProperty("payload").GetProperty("providerName").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        }
    }
}
