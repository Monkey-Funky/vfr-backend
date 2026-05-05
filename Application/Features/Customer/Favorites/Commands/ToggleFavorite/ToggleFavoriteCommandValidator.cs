namespace Application.Features.Customer.Favorites.Commands.ToggleFavorite;

public sealed class ToggleFavoriteCommandValidator : AbstractValidator<ToggleFavoriteCommand>
{
    public ToggleFavoriteCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product must be specified.");
    }
}
