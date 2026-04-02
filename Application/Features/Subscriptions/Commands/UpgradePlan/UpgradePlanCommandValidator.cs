namespace Application.Features.Subscriptions.Commands.UpgradePlan;

public sealed class UpgradePlanCommandValidator : AbstractValidator<UpgradePlanCommand>
{
    public UpgradePlanCommandValidator()
    {
        RuleFor(x => x.NewPlanId)
            .NotEmpty().WithMessage("New plan ID is required.");

        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("Payment method ID is required.");
    }
}
