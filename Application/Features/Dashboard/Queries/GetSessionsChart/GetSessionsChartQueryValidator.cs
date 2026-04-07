using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetSessionsChart;

public sealed class GetSessionsChartQueryValidator
    : DateRangeQueryValidator<GetSessionsChartQuery>
{
    public GetSessionsChartQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
        RuleFor(x => x.GroupBy).IsInEnum().WithMessage("GroupBy must be Day, Week, or Month.");
    }
}