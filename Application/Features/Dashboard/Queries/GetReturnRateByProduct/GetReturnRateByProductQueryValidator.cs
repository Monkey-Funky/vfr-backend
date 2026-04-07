using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetReturnRateByProduct;

public sealed class GetReturnRateByProductQueryValidator
    : DateRangeQueryValidator<GetReturnRateByProductQuery>
{
    public GetReturnRateByProductQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}