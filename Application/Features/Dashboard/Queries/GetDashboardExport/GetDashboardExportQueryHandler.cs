using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Dashboard.Queries.GetDashboardExport;

/// <summary>
/// Streams DashboardSnapshot rows as IAsyncEnumerable for CSV export.
/// No caching — exports are typically one-off downloads.
/// </summary>
public sealed class GetDashboardExportQueryHandler
    : IRequestHandler<GetDashboardExportQuery, IAsyncEnumerable<DashboardExportRow>>
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetDashboardExportQueryHandler(
        IDashboardRepository dashboardRepository,
        ICurrentUserService currentUserService)
    {
        _dashboardRepository = dashboardRepository;
        _currentUserService = currentUserService;
    }

    public Task<IAsyncEnumerable<DashboardExportRow>> Handle(
        GetDashboardExportQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        IAsyncEnumerable<DashboardExportRow> stream =
            _dashboardRepository.StreamExportRowsAsync(
                retailerId, query.From, query.To, cancellationToken);

        return Task.FromResult(stream);
    }
}