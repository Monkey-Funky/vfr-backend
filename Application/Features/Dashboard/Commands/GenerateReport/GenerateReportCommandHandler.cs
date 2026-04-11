using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;


namespace Application.Features.Dashboard.Commands.GenerateReport;

/// <summary>
/// Handles POST /api/retailers/{id}/dashboard/reports.
///
/// Steps:
///   1. Resolve retailer from JWT (IDOR enforced in DashboardController).
///   2. Create a Report record with Status = Pending.
///   3. Persist via IApplicationDbContext.Reports (NOT IUnitOfWork.Repository — Report
///      does not extend BaseEntity and therefore does not satisfy the generic constraint).
///   4. Enqueue the report ID in IReportQueue for background processing.
///   5. Return 202 Accepted + { reportId }.
///
/// </summary>
public sealed class GenerateReportCommandHandler
    : IRequestHandler<GenerateReportCommand, Result<GenerateReportResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IReportQueue _reportQueue;

    public GenerateReportCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IReportQueue reportQueue)
    {
        _context = context;
        _currentUserService = currentUserService;
        _reportQueue = reportQueue;
    }

    public async Task<Result<GenerateReportResponse>> Handle(
        GenerateReportCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var report = new Report(retailerId, command.From, command.To);

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        await _reportQueue.EnqueueAsync(report.Id, cancellationToken);

        return Result<GenerateReportResponse>.Success(new GenerateReportResponse(report.Id));
    }
}