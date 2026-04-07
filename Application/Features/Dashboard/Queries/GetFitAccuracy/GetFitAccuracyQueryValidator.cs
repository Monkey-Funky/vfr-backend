using Application.Features.Dashboard.Queries.Shared;


namespace Application.Features.Dashboard.Queries.GetFitAccuracy;

public sealed class GetFitAccuracyQueryValidator
    : DateRangeQueryValidator<GetFitAccuracyQuery>
{
    public GetFitAccuracyQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}