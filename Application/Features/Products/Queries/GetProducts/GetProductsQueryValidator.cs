using Domain.Enums.Product;

namespace Application.Features.Products.Queries.GetProducts;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(q => q.Status)
            .Must(s => s == null || ProductStatus.IsValid(s))
            .WithMessage($"Status must be one of: {string.Join(", ", ProductStatus.All)}.");

        RuleFor(q => q.SearchTerm)
            .MaximumLength(200)
            .WithMessage("Search term cannot exceed 200 characters.")
            .When(q => q.SearchTerm is not null);
    }
}