using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class FitFeedback : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? TryOnSessionId { get; private set; }
    public string? PredictedSize { get; private set; }
    public string? ActualSizeNeeded { get; private set; }
    public int FitRating { get; private set; }
    public string? FeedbackNotes { get; private set; }

    private FitFeedback() { }

    public static FitFeedback Create(
        Guid customerId,
        Guid orderItemId,
        Guid productId,
        int fitRating,
        string? predictedSize = null,
        string? actualSizeNeeded = null,
        string? feedbackNotes = null,
        Guid? tryOnSessionId = null)
    {
        if (fitRating < 1 || fitRating > 5)
            throw new BusinessRuleException("INVALID_RATING", "Fit rating must be between 1 and 5.");

        return new FitFeedback
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderItemId = orderItemId,
            ProductId = productId,
            TryOnSessionId = tryOnSessionId,
            PredictedSize = predictedSize,
            ActualSizeNeeded = actualSizeNeeded,
            FitRating = fitRating,
            FeedbackNotes = feedbackNotes
        };
    }
}
