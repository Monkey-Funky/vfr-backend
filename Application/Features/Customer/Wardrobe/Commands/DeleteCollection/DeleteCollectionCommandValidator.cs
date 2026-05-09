namespace Application.Features.Customer.Wardrobe.Commands.DeleteCollection;

public sealed class DeleteCollectionCommandValidator : AbstractValidator<DeleteCollectionCommand>
{
    public DeleteCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection must be specified.");
    }
}
