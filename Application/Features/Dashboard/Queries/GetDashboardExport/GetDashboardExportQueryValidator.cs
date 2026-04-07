using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Queries.GetDashboardExport;

public sealed class GetDashboardExportQueryValidator
    : DateRangeQueryValidator<GetDashboardExportQuery>
{
    public GetDashboardExportQueryValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}