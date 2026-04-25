using Domain.Enums.Customer;
using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class VirtualTryOnSession : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid RetailerId { get; private set; }
    public Guid? AvatarId { get; private set; }
    public TryOnSessionType SessionType { get; private set; }
    public SessionStatus Status { get; private set; } = SessionStatus.Processing;
    public string? RecommendedSize { get; private set; }
    public decimal? ConfidenceScore { get; private set; }
    public string? ResultImageUrl { get; private set; }
    public int? DurationSeconds { get; private set; }

    private VirtualTryOnSession() { }

    public static VirtualTryOnSession Create(
        Guid customerId,
        Guid productId,
        Guid retailerId,
        TryOnSessionType sessionType,
        Guid? avatarId = null)
    {

        return new VirtualTryOnSession
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ProductId = productId,
            RetailerId = retailerId,
            AvatarId = avatarId,
            SessionType = sessionType,
            Status = SessionStatus.Processing
        };
    }

    public void MarkAsCompleted(string resultImageUrl, string? recommendedSize, decimal? confidenceScore, int durationSeconds)
    {
        if (Status != SessionStatus.Processing)
            throw new BusinessRuleException("INVALID_STATUS", "Only processing sessions can be completed.");

        if (string.IsNullOrWhiteSpace(resultImageUrl))
            throw new BusinessRuleException("MISSING_IMAGE", "Result image URL is required for completion.");

        Status = SessionStatus.Completed;
        ResultImageUrl = resultImageUrl;
        RecommendedSize = recommendedSize;
        ConfidenceScore = confidenceScore;
        DurationSeconds = durationSeconds;
    }

    public void MarkAsFailed()
    {
        if (Status != SessionStatus.Processing)
            throw new BusinessRuleException("INVALID_STATUS", "Only processing sessions can be failed.");

        Status = SessionStatus.Failed;
    }
}
