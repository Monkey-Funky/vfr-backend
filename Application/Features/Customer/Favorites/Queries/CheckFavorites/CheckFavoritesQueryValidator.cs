namespace Application.Features.Customer.Favorites.Queries.CheckFavorites;

public sealed class CheckFavoritesQueryValidator : AbstractValidator<CheckFavoritesQuery>
{
    public CheckFavoritesQueryValidator()
    {
        RuleFor(x => x.ProductIds)
            .NotEmpty().WithMessage("At least one product must be selected.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("All product identifiers must be valid.");

        RuleFor(x => x.ProductIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("Duplicate product identifiers are not permitted.")
            .When(x => x.ProductIds is not null && x.ProductIds.Length > 0);
    }
}
