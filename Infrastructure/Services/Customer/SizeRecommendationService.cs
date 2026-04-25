using Application.Features.Customer.Avatar.DTOs;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Infrastructure.Services.Customer;

public sealed class SizeRecommendationService : ISizeRecommendationService
{
    public Task<SizeRecommendationDto> RecommendSizeAsync(
        Avatar customerAvatar, 
        Guid productId, 
        CancellationToken cancellationToken = default)
    {
        // Placeholder Logic for ML/AI Avatar-to-Product Size interpolation.
        // Assuming universal standardized sizing solely based on WaistCm for demo purposes.
        string recommendedSize = "M"; 
        decimal confidence = 0.65m;
        string justification = "Default recommendation. Provide more precise measurements for higher accuracy.";

        if (customerAvatar.WaistCm.HasValue)
        {
            var waist = customerAvatar.WaistCm.Value;
            if (waist < 75)
            {
                recommendedSize = "S";
            }
            else if (waist >= 75 && waist <= 85)
            {
                recommendedSize = "M";
            }
            else if (waist > 85 && waist <= 95)
            {
                recommendedSize = "L";
            }
            else
            {
                recommendedSize = "XL";
            }

            confidence = 0.85m;
            justification = $"Based on your waist measurement of {waist}cm compared to the product size chart.";
        }
        else if (customerAvatar.ChestCm.HasValue)
        {
            var chest = customerAvatar.ChestCm.Value;
            if (chest < 90) recommendedSize = "S";
            else if (chest >= 90 && chest <= 100) recommendedSize = "M";
            else if (chest > 100 && chest <= 110) recommendedSize = "L";
            else recommendedSize = "XL";

            confidence = 0.80m;
            justification = $"Based on your chest measurement of {chest}cm compared to the product size chart.";
        }

        return Task.FromResult(new SizeRecommendationDto(
            ProductId: productId,
            RecommendedSize: recommendedSize,
            ConfidenceScore: confidence,
            Justification: justification
        ));
    }
}
