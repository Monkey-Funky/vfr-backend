namespace Application.Features.Customer.Wardrobe.Commands.AddItemToCollection;

public sealed class AddItemToCollectionCommandValidator : AbstractValidator<AddItemToCollectionCommand>
{
    public AddItemToCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection must be specified.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product must be specified.");
    }
}
