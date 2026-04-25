namespace Application.Features.Customer.FitFeedback.DTOs;

public sealed record FitFeedbackDto(
    Guid Id,
    Guid CustomerId,
    Guid OrderItemId,
    Guid ProductId,
    Guid? TryOnSessionId,
    string? PredictedSize,
    string? ActualSizeNeeded,
    int FitRating,
    string? FeedbackNotes,
    DateTime CreatedAt
);
