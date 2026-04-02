namespace Application.Features.PaymentMethods.Commands.AddPaymentMethod;

public sealed class AddPaymentMethodCommandValidator
    : AbstractValidator<AddPaymentMethodCommand>
{
    private static readonly string[] ValidProviders =
    [
        "Visa", "Mastercard", "PayPal", "ApplePay", "Stripe", "GooglePay", "Bitpay"
    ];

    public AddPaymentMethodCommandValidator()
    {
        RuleFor(x => x.ProviderType)
            .NotEmpty().WithMessage("Provider type is required.")
            .Must(p => ValidProviders.Contains(p))
            .WithMessage(
                $"Provider type must be one of: {string.Join(", ", ValidProviders)}.");

        RuleFor(x => x.CardholderName)
            .NotEmpty().WithMessage("Cardholder name is required.")
            .MaximumLength(100).WithMessage("Cardholder name must not exceed 100 characters.");

        RuleFor(x => x.CardNumberLast4)
            .NotEmpty().WithMessage("Card last 4 digits are required.")
            .Length(4).WithMessage("CardNumberLast4 must be exactly 4 digits.")
            .Matches(@"^\d{4}$").WithMessage("CardNumberLast4 must contain only digits.");

        RuleFor(x => x.ExpiryDate)
            .NotEmpty().WithMessage("Expiry date is required.")
            .Matches(@"^(0[1-9]|1[0-2])\/\d{4}$")
            .WithMessage("Expiry date must be in MM/YYYY format (e.g., 12/2027).");
    }
}