using System.Net;
using System.Net.Http.Json;
using Maliev.PaymentService.Infrastructure.Providers;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Providers;

public sealed class StripeProviderTests
{
    [Fact]
    public async Task ProcessPaymentAsync_CreatesCheckoutSessionWithPaymentMetadata()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "cs_test_123",
                url = "https://checkout.stripe.com/c/pay/cs_test_123",
                status = "open"
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new StripeProvider(httpClient, "sk_test_123", "https://api.stripe.com");

        var result = await provider.ProcessPaymentAsync(new ProviderPaymentRequest
        {
            IdempotencyKey = "idem-stripe-123",
            Amount = 1234.56m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success?orderId=ORD-456",
            CancelUrl = "https://quote.example.com/payment/cancel?orderId=ORD-456",
            Metadata = new Dictionary<string, string>
            {
                ["transactionId"] = "tx-789",
                ["orderNumber"] = "ORD-456"
            }
        });

        Assert.True(result.Success);
        Assert.Equal("cs_test_123", result.ProviderTransactionId);
        Assert.Equal("processing", result.Status);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_123", result.PaymentUrl);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("https://api.stripe.com/v1/checkout/sessions", handler.Request.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("sk_test_123", handler.Request.Headers.Authorization?.Parameter);
        Assert.True(handler.Request.Headers.TryGetValues("Idempotency-Key", out var idempotencyValues));
        Assert.Equal("idem-stripe-123", Assert.Single(idempotencyValues));

        var form = ReadForm(handler.RequestBody);
        Assert.Equal("payment", form["mode"]);
        Assert.Equal("https://quote.example.com/payment/success?orderId=ORD-456", form["success_url"]);
        Assert.Equal("https://quote.example.com/payment/cancel?orderId=ORD-456", form["cancel_url"]);
        Assert.Equal("order-456", form["client_reference_id"]);
        Assert.Equal("thb", form["line_items[0][price_data][currency]"]);
        Assert.Equal("123456", form["line_items[0][price_data][unit_amount]"]);
        Assert.Equal("Manufacturing order ORD-456", form["line_items[0][price_data][product_data][name]"]);
        Assert.Equal("1", form["line_items[0][quantity]"]);
        Assert.Equal("required", form["billing_address_collection"]);
        Assert.Equal("always", form["customer_creation"]);
        Assert.Equal("true", form["phone_number_collection[enabled]"]);
        Assert.Equal("true", form["tax_id_collection[enabled]"]);
        Assert.Equal("true", form["name_collection[individual][enabled]"]);
        Assert.Equal("false", form["name_collection[individual][optional]"]);
        Assert.Equal("true", form["name_collection[business][enabled]"]);
        Assert.Equal("true", form["name_collection[business][optional]"]);
        Assert.Equal("required", form["consent_collection[terms_of_service]"]);
        Assert.Equal("TH", form["shipping_address_collection[allowed_countries][0]"]);
        Assert.Equal("tx-789", form["metadata[transactionId]"]);
        Assert.Equal("ORD-456", form["metadata[orderNumber]"]);
        Assert.Equal("customer-123", form["metadata[customerId]"]);
        Assert.Equal("order-456", form["metadata[orderId]"]);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_RetrievesCheckoutSessionAndMapsPaidStatus()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "cs_test_123",
                status = "complete",
                payment_status = "paid"
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new StripeProvider(httpClient, "sk_test_123", "https://api.stripe.com");

        var result = await provider.GetPaymentStatusAsync("cs_test_123");

        Assert.Equal("succeeded", result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Equal("https://api.stripe.com/v1/checkout/sessions/cs_test_123", handler.Request.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("sk_test_123", handler.Request.Headers.Authorization?.Parameter);
    }

    private static Dictionary<string, string> ReadForm(string? body)
    {
        Assert.False(string.IsNullOrWhiteSpace(body));
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => WebUtility.UrlDecode(parts[0]),
                parts => parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : string.Empty);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
