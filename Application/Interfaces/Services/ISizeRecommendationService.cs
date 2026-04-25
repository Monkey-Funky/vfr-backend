using Application.Features.Customer.Avatar.DTOs;
using Domain.Entities.Customer;

namespace Application.Interfaces.Services;

public interface ISizeRecommendationService
{
    Task<SizeRecommendationDto> RecommendSizeAsync(
        Avatar customerAvatar,
        Guid productId,
        CancellationToken cancellationToken = default);
}
