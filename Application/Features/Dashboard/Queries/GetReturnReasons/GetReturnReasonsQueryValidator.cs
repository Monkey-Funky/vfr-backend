using Application.Features.Dashboard.Queries.Shared;


namespace Application.Features.Dashboard.Queries.GetReturnReasons;
public sealed class GetReturnReasonsQueryValidator
    : DateRangeQueryValidator<GetReturnReasonsQuery>
{
    public GetReturnReasonsQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}