namespace Application.Features.Subscriptions.Commands.DowngradePlan;

public sealed class DowngradePlanCommandValidator : AbstractValidator<DowngradePlanCommand>
{
    public DowngradePlanCommandValidator()
    {
        RuleFor(x => x.NewPlanId)
            .NotEmpty().WithMessage("New plan ID is required.");
    }
}