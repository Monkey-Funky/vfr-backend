using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetConversionRate;

public sealed class GetConversionRateQueryValidator
    : DateRangeQueryValidator<GetConversionRateQuery>
{
    public GetConversionRateQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}