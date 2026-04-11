using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetRealTimeActivity;

/// <summary>
/// Returns the last N activity events for the authenticated retailer (N ≤ 50).
/// No Redis cache — must be live data. Short 30-second TTL applied as a floor guard.
/// </summary>
public sealed record GetRealTimeActivityQuery : IRequest<List<ActivityEventDto>>;
