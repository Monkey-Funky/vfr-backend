using Application.Features.Dashboard.Queries.Shared;


namespace Application.Features.Dashboard.Queries.GetProfitChart;

public sealed class GetProfitChartQueryValidator
    : DateRangeQueryValidator<GetProfitChartQuery>
{
    public GetProfitChartQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
        RuleFor(x => x.GroupBy).IsInEnum().WithMessage("GroupBy must be Day, Week, or Month.");
    }
}