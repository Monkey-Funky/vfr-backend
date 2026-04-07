using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetRevenueChart;

public sealed class GetRevenueChartQueryValidator
    : DateRangeQueryValidator<GetRevenueChartQuery>
{
    public GetRevenueChartQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);

        RuleFor(x => x.GroupBy)
            .IsInEnum()
            .WithMessage("GroupBy must be one of: Day, Week, Month.");
    }
}