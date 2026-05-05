namespace Application.Features.Customer.Wardrobe.Commands.CreateCollection;

public sealed class CreateCollectionCommandValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Collection name is required.")
            .MaximumLength(100).WithMessage("Collection name must not exceed 100 characters.");
    }
}
