using Application.Features.Dashboard.DTOs;

namespace Application.Features.Dashboard.Queries.GetDashboardExport;

/// <summary>
/// Returns dashboard data as an async stream for CSV export.
/// The result is IAsyncEnumerable to avoid loading all rows into memory.
/// Controllers must consume the stream with `await foreach` and write to the response body.
/// </summary>
public sealed record GetDashboardExportQuery(
    DateOnly From,
    DateOnly To
) : IRequest<IAsyncEnumerable<DashboardExportRow>>;