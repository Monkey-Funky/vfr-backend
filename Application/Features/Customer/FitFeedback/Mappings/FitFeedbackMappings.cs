using Application.Features.Customer.FitFeedback.DTOs;

namespace Application.Features.Customer.FitFeedback.Mappings;

public static class FitFeedbackMappings
{
    public static FitFeedbackDto ToDto(this Domain.Entities.Customer.FitFeedback feedback)
    {
        return new FitFeedbackDto(
            Id: feedback.Id,
            CustomerId: feedback.CustomerId,
            OrderItemId: feedback.OrderItemId,
            ProductId: feedback.ProductId,
            TryOnSessionId: feedback.TryOnSessionId,
            PredictedSize: feedback.PredictedSize,
            ActualSizeNeeded: feedback.ActualSizeNeeded,
            FitRating: feedback.FitRating,
            FeedbackNotes: feedback.FeedbackNotes,
            CreatedAt: feedback.CreatedAt
        );
    }
}
