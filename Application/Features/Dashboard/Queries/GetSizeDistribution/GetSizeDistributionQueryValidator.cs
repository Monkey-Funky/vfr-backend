using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetSizeDistribution;

public sealed class GetSizeDistributionQueryValidator
    : DateRangeQueryValidator<GetSizeDistributionQuery>
{
    public GetSizeDistributionQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}