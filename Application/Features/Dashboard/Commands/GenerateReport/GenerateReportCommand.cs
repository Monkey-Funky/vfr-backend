using Application.Features.Dashboard.DTOs;

namespace Application.Features.Dashboard.Commands.GenerateReport;

public sealed record GenerateReportCommand(
    DateOnly From,
    DateOnly To
) : IRequest<Result<GenerateReportResponse>>;