using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetConversionRate;


public sealed record GetConversionRateQuery(
    DateOnly From,
    DateOnly To
) : IRequest<ConversionRateDto>;
