using FluentValidation;
using Maliev.PaymentService.Api.Models.Requests;

namespace Maliev.PaymentService.Api.Validators;

/// <summary>
/// Validator for PaymentRequest.
/// Ensures request data meets business rules before processing.
/// </summary>
public class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(999999999.99m)
            .WithMessage("Amount exceeds maximum allowed value");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-letter ISO code")
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters only (e.g., USD, EUR, THB)");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required")
            .MaximumLength(100)
            .WithMessage("CustomerId cannot exceed 100 characters");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required")
            .MaximumLength(100)
            .WithMessage("OrderId cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.ReturnUrl)
            .NotEmpty()
            .WithMessage("ReturnUrl is required")
            .Must(BeAValidUrl)
            .WithMessage("ReturnUrl must be a valid HTTPS URL");

        RuleFor(x => x.CancelUrl)
            .NotEmpty()
            .WithMessage("CancelUrl is required")
            .Must(BeAValidUrl)
            .WithMessage("CancelUrl must be a valid HTTPS URL");

        RuleFor(x => x.PreferredProvider)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.PreferredProvider))
            .WithMessage("PreferredProvider cannot exceed 50 characters");
    }

    private bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && uriResult.Scheme == Uri.UriSchemeHttps;
    }
}
