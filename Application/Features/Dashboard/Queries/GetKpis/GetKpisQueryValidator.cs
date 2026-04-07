using Application.Features.Dashboard.Queries.Shared;


namespace Application.Features.Dashboard.Queries.GetKpis;

public sealed class GetKpisQueryValidator : DateRangeQueryValidator<GetKpisQuery>
{
    public GetKpisQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}