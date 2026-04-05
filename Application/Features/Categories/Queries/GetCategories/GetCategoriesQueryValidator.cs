using System;
using static Domain.Entities.Retailer.Category;

namespace Application.Features.Categories.Queries.GetCategories;


public sealed class GetCategoriesQueryValidator
    : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.Status)
            .Must(s => s is null
                       || s == CategoryStatus.Active
                       || s == CategoryStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive' if provided.")
            .When(x => x.Status is not null);
    }
}