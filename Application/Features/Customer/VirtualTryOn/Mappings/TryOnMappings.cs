using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Entities.Customer;

namespace Application.Features.Customer.VirtualTryOn.Mappings;

public static class TryOnMappings
{
    public static VirtualTryOnSessionDto ToDto(this VirtualTryOnSession session)
    {
        return new VirtualTryOnSessionDto(
            Id: session.Id,
            CustomerId: session.CustomerId,
            ProductId: session.ProductId,
            RetailerId: session.RetailerId,
            AvatarId: session.AvatarId,
            SessionType: session.SessionType.ToString(),
            Status: session.Status.ToString(),
            RecommendedSize: session.RecommendedSize,
            ConfidenceScore: session.ConfidenceScore,
            ResultImageUrl: session.ResultImageUrl,
            DurationSeconds: session.DurationSeconds,
            CreatedAt: session.CreatedAt
        );
    }
}
