using Application.Features.Dashboard.DTOs;

namespace Application.Features.Dashboard.Queries.GetReportStatus;

public sealed record GetReportStatusQuery(Guid ReportId) : IRequest<ReportStatusDto>;
