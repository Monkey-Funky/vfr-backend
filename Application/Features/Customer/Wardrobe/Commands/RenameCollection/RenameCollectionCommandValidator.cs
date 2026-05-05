namespace Application.Features.Customer.Wardrobe.Commands.RenameCollection;

public sealed class RenameCollectionCommandValidator : AbstractValidator<RenameCollectionCommand>
{
    public RenameCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection must be specified.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("Collection name is required.")
            .MaximumLength(100).WithMessage("Collection name must not exceed 100 characters.");
    }
}
