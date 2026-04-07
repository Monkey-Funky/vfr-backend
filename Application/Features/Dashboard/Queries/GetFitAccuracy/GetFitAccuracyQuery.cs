using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetFitAccuracy;

public sealed record GetFitAccuracyQuery(
    DateOnly From,
    DateOnly To
) : IRequest<FitAccuracyDto>;