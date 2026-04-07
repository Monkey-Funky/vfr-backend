using Application.Features.Dashboard.DTOs;
using Application.Features.Dashboard.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Dashboard.Queries.GetReportStatus;

public sealed class GetReportStatusQueryHandler
    : IRequestHandler<GetReportStatusQuery, ReportStatusDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetReportStatusQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ReportStatusDto> Handle(
        GetReportStatusQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // RetailerId scoping is the IDOR guard — a report that exists but belongs
        // to another retailer returns NotFoundException, not a 403.
        Report report = await _context.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == query.ReportId && r.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Report), query.ReportId);

        return report.ToStatusDto();
    }
}
