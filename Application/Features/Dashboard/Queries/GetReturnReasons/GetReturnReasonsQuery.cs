using Application.Features.Dashboard.DTOs;

namespace Application.Features.Dashboard.Queries.GetReturnReasons;

public sealed record GetReturnReasonsQuery(
    DateOnly From,
    DateOnly To
) : IRequest<List<ReturnReasonDto>>;