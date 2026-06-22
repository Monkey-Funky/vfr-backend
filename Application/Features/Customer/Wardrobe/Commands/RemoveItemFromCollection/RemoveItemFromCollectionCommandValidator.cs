namespace Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;

public sealed class RemoveItemFromCollectionCommandValidator : AbstractValidator<RemoveItemFromCollectionCommand>
{
    public RemoveItemFromCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection must be specified.");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item must be specified.");
    }
}