namespace Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;

public sealed class InitiateTryOnCommandValidator : AbstractValidator<InitiateTryOnCommand>
{
    public InitiateTryOnCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required.");
        
        RuleFor(x => x.SessionType)
            .IsInEnum()
            .WithMessage("SessionType must be a valid TryOnSessionType.");
    }
}
