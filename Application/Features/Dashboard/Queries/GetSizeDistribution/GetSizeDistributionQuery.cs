using Application.Features.Dashboard.DTOs;

namespace Application.Features.Dashboard.Queries.GetSizeDistribution;

public sealed record GetSizeDistributionQuery(
    DateOnly From,
    DateOnly To
) : IRequest<List<SizeDistributionDto>>;