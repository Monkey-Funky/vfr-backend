using Domain.Enums.Orders;

namespace Application.Features.Orders.Queries.GetOrders;

public sealed class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public GetOrdersQueryValidator()
    {
        RuleFor(q => q.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(q => q.Status)
            .Must(s => s is null || OrderStatus.IsValid(s))
            .WithMessage($"Status must be one of: {string.Join(", ", OrderStatus.All)}.");

        RuleFor(q => q.SearchTerm)
            .MaximumLength(200).WithMessage("SearchTerm must not exceed 200 characters.")
            .When(q => q.SearchTerm is not null);
    }
}
