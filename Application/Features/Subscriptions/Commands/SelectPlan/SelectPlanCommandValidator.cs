namespace Application.Features.Subscriptions.Commands.SelectPlan;

public sealed class SelectPlanCommandValidator : AbstractValidator<SelectPlanCommand>
{
    private static readonly string[] ValidBillingCycles = ["Monthly", "Yearly", "SaaS"];

    public SelectPlanCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Plan ID is required.");

        RuleFor(x => x.PaymentMethodId)
            .NotEmpty().WithMessage("Payment method ID is required.");

        RuleFor(x => x.BillingCycle)
            .NotEmpty().WithMessage("Billing cycle is required.")
            .Must(bc => ValidBillingCycles.Contains(bc))
            .WithMessage($"Billing cycle must be one of: {string.Join(", ", ValidBillingCycles)}.");
    }
}