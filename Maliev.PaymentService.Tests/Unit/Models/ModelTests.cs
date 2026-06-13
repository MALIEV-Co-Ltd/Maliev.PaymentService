using Maliev.PaymentService.Api.Models.Requests;
using Maliev.PaymentService.Api.Models.Responses;
using Maliev.PaymentService.Domain.Enums;
using Xunit;

namespace Maliev.PaymentService.Tests.Unit.Models;

public class ModelTests
{
    [Fact]
    public void PaymentRequest_PropertyTest()
    {
        var model = new PaymentRequest
        {
            Amount = 100,
            Currency = "USD",
            CustomerId = "cust1",
            OrderId = "order1",
            Description = "desc",
            PreferredProvider = "stripe",
            ReturnUrl = "http://return",
            CancelUrl = "http://cancel",
            Metadata = new Dictionary<string, string> { { "key", "value" } }
        };

        Assert.Equal(100, model.Amount);
        Assert.Equal("USD", model.Currency);
        Assert.Equal("cust1", model.CustomerId);
        Assert.Equal("order1", model.OrderId);
        Assert.Equal("desc", model.Description);
        Assert.Equal("stripe", model.PreferredProvider);
        Assert.Equal("http://return", model.ReturnUrl);
        Assert.Equal("http://cancel", model.CancelUrl);
        Assert.Single(model.Metadata);
    }

    [Fact]
    public void PaymentResponse_PropertyTest()
    {
        var id = Guid.NewGuid();
        var model = new PaymentResponse
        {
            TransactionId = id,
            Amount = 100,
            Currency = "USD",
            Status = PaymentStatus.Completed,
            ProviderTransactionId = "pt1",
            SelectedProvider = "stripe",
            CustomerId = "cust1",
            OrderId = "order1",
            PaymentUrl = "http://pay",
            Metadata = new Dictionary<string, string> { { "k", "v" } },
            Description = "desc",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal(id, model.TransactionId);
        Assert.Equal(PaymentStatus.Completed, model.Status);
    }

    [Fact]
    public void RegisterProviderRequest_PropertyTest()
    {
        var model = new RegisterProviderRequest
        {
            Name = "omise",
            DisplayName = "Omise",
            SupportedCurrencies = new List<string> { "THB" },
            Priority = 2,
            Credentials = new Dictionary<string, string> { { "ApiKey", "key" } },
            Status = ProviderStatus.Active
        };

        Assert.Equal("omise", model.Name);
        Assert.Equal(2, model.Priority);
    }

    [Fact]
    public void UpdateProviderRequest_PropertyTest()
    {
        var model = new UpdateProviderRequest
        {
            DisplayName = "New Name",
            SupportedCurrencies = new List<string> { "EUR" },
            Priority = 5,
            Credentials = new Dictionary<string, string> { { "Secret", "sec" } }
        };

        Assert.Equal("New Name", model.DisplayName);
        Assert.Equal(5, model.Priority);
    }

    [Fact]
    public void RefundRequest_PropertyTest()
    {
        var model = new RefundRequest
        {
            Amount = 50,
            Reason = "User request",
            RefundType = "full"
        };

        Assert.Equal(50, model.Amount);
        Assert.Equal("User request", model.Reason);
    }

    [Fact]
    public void ErrorResponse_PropertyTest()
    {
        var now = DateTime.UtcNow;
        var details = new Dictionary<string, object> { { "field", "error" } };
        var model = new ErrorResponse
        {
            Error = "CODE",
            Message = "Msg",
            Details = details,
            Timestamp = now
        };

        Assert.Equal("CODE", model.Error);
        Assert.Equal("Msg", model.Message);
        Assert.Equal(details, model.Details);
        Assert.Equal(now, model.Timestamp);
    }

    [Fact]
    public void PaymentStatusResponse_PropertyTest()
    {
        var model = new PaymentStatusResponse
        {
            TransactionId = Guid.NewGuid(),
            Status = "Completed",
            Amount = 100,
            Currency = "USD",
            Provider = "stripe",
            ProviderReference = "ref1",
            ErrorCode = "E1",
            ErrorMessage = "Err",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string> { { "a", "b" } }
        };

        Assert.Equal("Completed", model.Status);
        Assert.Equal(100, model.Amount);
    }

    [Fact]
    public void UpdateProviderStatusRequest_PropertyTest()
    {
        var model = new UpdateProviderStatusRequest
        {
            Status = ProviderStatus.Disabled
        };

        Assert.Equal(ProviderStatus.Disabled, model.Status);
    }

    [Fact]
    public void PaymentRequest_Validate_InvalidUrls()
    {
        var model = new PaymentRequest
        {
            Amount = 100,
            Currency = "USD",
            CustomerId = "c",
            OrderId = "o",
            Description = "d",
            ReturnUrl = "http://insecure",
            CancelUrl = "http://insecure"
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("ReturnUrl must be a valid HTTPS URL"));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("CancelUrl must be a valid HTTPS URL"));
    }

    [Fact]
    public void PaymentRequest_Validate_TooManyMetadataEntries_ReturnsValidationError()
    {
        var model = CreateValidPaymentRequest();
        model.Metadata = Enumerable.Range(1, 51)
            .ToDictionary(index => $"key{index}", index => $"value{index}");

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Metadata cannot contain more than 50 entries"));
        Assert.Contains(results[0].MemberNames, memberName => memberName == nameof(PaymentRequest.Metadata));
    }

    [Fact]
    public void PaymentRequest_Validate_OversizedMetadataKey_ReturnsValidationError()
    {
        var model = CreateValidPaymentRequest();
        model.Metadata = new Dictionary<string, string>
        {
            [new string('k', 41)] = "value"
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Metadata keys cannot exceed 40 characters"));
        Assert.Contains(results[0].MemberNames, memberName => memberName == nameof(PaymentRequest.Metadata));
    }

    [Fact]
    public void PaymentRequest_Validate_MetadataKeyWithSquareBrackets_ReturnsValidationError()
    {
        var model = CreateValidPaymentRequest();
        model.Metadata = new Dictionary<string, string>
        {
            ["order[number]"] = "Q-2026-0001"
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Metadata keys cannot contain square brackets"));
        Assert.Contains(results[0].MemberNames, memberName => memberName == nameof(PaymentRequest.Metadata));
    }

    [Fact]
    public void PaymentRequest_Validate_OversizedMetadataValue_ReturnsValidationError()
    {
        var model = CreateValidPaymentRequest();
        model.Metadata = new Dictionary<string, string>
        {
            ["key"] = new string('v', 501)
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Metadata values cannot exceed 500 characters"));
        Assert.Contains(results[0].MemberNames, memberName => memberName == nameof(PaymentRequest.Metadata));
    }

    [Fact]
    public void PaymentRequest_Validate_ProviderSafeMetadata_ReturnsNoMetadataErrors()
    {
        var model = CreateValidPaymentRequest();
        model.Metadata = new Dictionary<string, string>
        {
            ["orderNumber"] = "Q-2026-0001",
            ["billingAddressId"] = Guid.NewGuid().ToString("D"),
            ["shippingAddressId"] = Guid.NewGuid().ToString("D"),
            ["acceptedTerms"] = "true"
        };

        var context = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var results = model.Validate(context).ToList();

        Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(PaymentRequest.Metadata)));
    }

    private static PaymentRequest CreateValidPaymentRequest()
    {
        return new PaymentRequest
        {
            Amount = 100,
            Currency = "USD",
            CustomerId = "customer-1",
            OrderId = "order-1",
            Description = "Quote payment",
            ReturnUrl = "https://quote.example.com/payment/success",
            CancelUrl = "https://quote.example.com/payment/cancel"
        };
    }
}
