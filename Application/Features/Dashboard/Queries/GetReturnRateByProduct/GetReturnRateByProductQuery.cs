using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetReturnRateByProduct;

public sealed record GetReturnRateByProductQuery(
    DateOnly From,
    DateOnly To
) : IRequest<List<ReturnRateByProductDto>>;