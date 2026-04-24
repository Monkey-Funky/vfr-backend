using Application.Features.Customer.Avatar.DTOs;


namespace Application.Features.Customer.Avatar.Queries.GetSizeRecommendation;

public sealed record GetSizeRecommendationQuery(Guid ProductId) : IRequest<SizeRecommendationDto>;
