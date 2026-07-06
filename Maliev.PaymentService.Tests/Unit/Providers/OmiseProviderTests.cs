using System.Net;
using System.Net.Http.Json;
using System.Text;
using Maliev.PaymentService.Infrastructure.Providers;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Providers;

public sealed class OmiseProviderTests
{
    [Fact]
    public async Task ProcessPaymentAsync_CreatesPromptPayChargeWithMetadata()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "chrg_test_123",
                status = "pending",
                authorize_uri = "https://pay.omise.co/payments/paym_test_123",
                source = new
                {
                    scannable_code = new
                    {
                        image = new
                        {
                            download_uri = "https://api.omise.co/charges/chrg_test_123/documents/docu_test/downloads/png"
                        }
                    }
                }
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new OmiseProvider(httpClient, "skey_test_123", "https://api.omise.co");

        var result = await provider.ProcessPaymentAsync(new ProviderPaymentRequest
        {
            IdempotencyKey = "omise-idem-123",
            Amount = 1500.25m,
            Currency = "THB",
            CustomerId = "customer-123",
            OrderId = "order-456",
            Description = "Manufacturing order ORD-456",
            ReturnUrl = "https://quote.example.com/payment/success",
            CancelUrl = "https://quote.example.com/payment/cancel",
            Metadata = new Dictionary<string, string>
            {
                ["transactionId"] = "tx-789",
                ["orderNumber"] = "ORD-456"
            }
        });

        Assert.True(result.Success);
        Assert.Equal("chrg_test_123", result.ProviderTransactionId);
        Assert.Equal("pending", result.Status);
        Assert.Equal("https://pay.omise.co/payments/paym_test_123", result.PaymentUrl);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("https://api.omise.co/charges", handler.Request.RequestUri?.ToString());
        Assert.Equal("Basic", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("skey_test_123:")),
            handler.Request.Headers.Authorization?.Parameter);
        Assert.True(handler.Request.Headers.TryGetValues("Idempotency-Key", out var idempotencyHeaders));
        Assert.Equal("omise-idem-123", Assert.Single(idempotencyHeaders));

        var form = ReadForm(handler.RequestBody);
        Assert.Equal("150025", form["amount"]);
        Assert.Equal("thb", form["currency"]);
        Assert.Equal("promptpay", form["source[type]"]);
        Assert.Equal("https://quote.example.com/payment/success", form["return_uri"]);
        Assert.Equal("Manufacturing order ORD-456", form["description"]);
        Assert.Equal("tx-789", form["metadata[transactionId]"]);
        Assert.Equal("ORD-456", form["metadata[orderNumber]"]);
        Assert.Equal("customer-123", form["metadata[customerId]"]);
        Assert.Equal("order-456", form["metadata[orderId]"]);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ExposesPromptPayQrDetails()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "chrg_test_qr",
                status = "pending",
                expires_at = "2026-07-06T12:00:00Z",
                source = new
                {
                    scannable_code = new
                    {
                        raw_data = "00020101021129370016A000000677010111-6304ABCD",
                        image = new
                        {
                            download_uri = "https://api.omise.co/charges/chrg_test_qr/documents/docu_qr/downloads/png"
                        }
                    }
                }
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new OmiseProvider(httpClient, "skey_test_123", "https://api.omise.co");

        var result = await provider.ProcessPaymentAsync(new ProviderPaymentRequest
        {
            IdempotencyKey = "omise-idem-qr",
            Amount = 12500m,
            Currency = "THB",
            CustomerId = "customer-1",
            OrderId = "order-1",
            Description = "PromptPay order",
            ReturnUrl = "https://quote.example.com/payment/success",
            CancelUrl = "https://quote.example.com/payment/cancel"
        });

        Assert.True(result.Success);
        Assert.Equal("promptpay", result.PaymentMethod);
        Assert.Equal("https://api.omise.co/charges/chrg_test_qr/documents/docu_qr/downloads/png", result.QrImageUrl);
        Assert.Equal("00020101021129370016A000000677010111-6304ABCD", result.QrRawData);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_RetrievesChargeStatus()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "chrg_test_123",
                status = "successful"
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new OmiseProvider(httpClient, "skey_test_123", "https://api.omise.co");

        var result = await provider.GetPaymentStatusAsync("chrg_test_123");

        Assert.Equal("successful", result.Status);
        Assert.NotNull(result.CompletedAt);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Equal("https://api.omise.co/charges/chrg_test_123", handler.Request.RequestUri?.ToString());
    }

    [Fact]
    public async Task ProcessRefundAsync_CreatesChargeRefundWithIdempotencyMetadata()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "rfnd_test_123",
                status = "closed"
            })
        });
        using var httpClient = new HttpClient(handler);
        var provider = new OmiseProvider(httpClient, "skey_test_123", "https://api.omise.co");

        var result = await provider.ProcessRefundAsync(new ProviderRefundRequest
        {
            IdempotencyKey = "refund-idem-123",
            ProviderTransactionId = "chrg_test_123",
            Amount = 120.50m,
            Currency = "THB",
            Reason = "Customer requested refund",
            Metadata = new Dictionary<string, string>
            {
                ["refundId"] = "refund-123",
                ["orderNumber"] = "ORD-789"
            }
        });

        Assert.True(result.Success);
        Assert.Equal("rfnd_test_123", result.ProviderRefundId);
        Assert.Equal("closed", result.Status);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("https://api.omise.co/charges/chrg_test_123/refunds", handler.Request.RequestUri?.ToString());
        Assert.True(handler.Request.Headers.TryGetValues("Idempotency-Key", out var idempotencyHeaders));
        Assert.Equal("refund-idem-123", Assert.Single(idempotencyHeaders));

        var form = ReadForm(handler.RequestBody);
        Assert.Equal("12050", form["amount"]);
        Assert.Equal("Customer requested refund", form["metadata[reason]"]);
        Assert.Equal("refund-idem-123", form["metadata[idempotencyKey]"]);
        Assert.Equal("refund-123", form["metadata[refundId]"]);
        Assert.Equal("ORD-789", form["metadata[orderNumber]"]);
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
