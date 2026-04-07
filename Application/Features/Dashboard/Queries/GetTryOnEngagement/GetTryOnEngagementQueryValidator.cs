using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetTryOnEngagement;

public sealed class GetTryOnEngagementQueryValidator
    : DateRangeQueryValidator<GetTryOnEngagementQuery>
{
    public GetTryOnEngagementQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
        RuleFor(x => x.GroupBy).IsInEnum().WithMessage("GroupBy must be Day, Week, or Month.");
    }
}