using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetKpis;

public sealed record GetKpisQuery(
    DateOnly From,
    DateOnly To
) : IRequest<KpiDto>;