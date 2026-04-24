using Application.Features.Customer.FitFeedback.DTOs;

namespace Application.Features.Customer.FitFeedback.Commands.SubmitFitFeedback;

public sealed record SubmitFitFeedbackCommand(
    Guid OrderItemId,
    Guid ProductId,
    int FitRating,
    string? PredictedSize,
    string? ActualSizeNeeded,
    string? FeedbackNotes,
    Guid? TryOnSessionId) : IRequest<FitFeedbackDto>;
