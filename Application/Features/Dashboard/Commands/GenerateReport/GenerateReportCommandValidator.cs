using Application.Features.Dashboard.Queries.Shared;

namespace Application.Features.Dashboard.Commands.GenerateReport;
public sealed class GenerateReportCommandValidator
    : DateRangeQueryValidator<GenerateReportCommand>
{
    public GenerateReportCommandValidator()
    {
        ApplyDateRangeRules(x => x.From, x => x.To);
    }
}