using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Entities.Customer;
using Domain.Enums.Customer;

namespace Application.Features.Customer.VirtualTryOn.Mappings;

public static class TryOnMappings
{
    public static VirtualTryOnSessionDto ToDto(this VirtualTryOnSession session)
    {
        // The entity stores the result URL in ResultImageUrl for all session types.
        // For 3D sessions the stored URL is the model URL — route it to the correct DTO field.
        var is3D = session.SessionType == TryOnSessionType.Model3D
                || session.SessionType == TryOnSessionType.ARLiveView;

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
            ResultImageUrl: is3D ? null : session.ResultImageUrl,
            ResultModelUrl: is3D ? session.ResultImageUrl : null,
            DurationSeconds: session.DurationSeconds,
            CreatedAt: session.CreatedAt
        );
    }
}
