
namespace Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;

public sealed class GetCollectionItemsQueryValidator : AbstractValidator<GetCollectionItemsQuery>
{
    public GetCollectionItemsQueryValidator()
    {
        RuleFor(x => x.CollectionId)
            .NotEmpty().WithMessage("Collection must be specified.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
