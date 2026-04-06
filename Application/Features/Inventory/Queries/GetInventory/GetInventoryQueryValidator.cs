
namespace Application.Features.Inventory.Queries.GetInventory;

public sealed class GetInventoryQueryValidator : AbstractValidator<GetInventoryQuery>
{
    public GetInventoryQueryValidator()
    {
        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(q => q.ProductName)
            .MaximumLength(200)
            .WithMessage("ProductName filter must not exceed 200 characters.")
            .When(q => q.ProductName is not null);
    }
}