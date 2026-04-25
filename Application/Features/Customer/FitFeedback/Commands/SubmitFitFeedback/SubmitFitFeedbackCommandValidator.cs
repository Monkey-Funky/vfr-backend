namespace Application.Features.Customer.FitFeedback.Commands.SubmitFitFeedback;

public sealed class SubmitFitFeedbackCommandValidator : AbstractValidator<SubmitFitFeedbackCommand>
{
    public SubmitFitFeedbackCommandValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty().WithMessage("Order item ID is required.");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required.");
        RuleFor(x => x.FitRating).InclusiveBetween(1, 5).WithMessage("Fit rating must be between 1 and 5.");
    }
}
