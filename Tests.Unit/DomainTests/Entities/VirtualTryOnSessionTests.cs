using Domain.Enums.Customer;

namespace Tests.Unit.DomainTests.Entities;

public sealed class VirtualTryOnSessionTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private static readonly Guid ValidProductId = Guid.NewGuid();
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private static readonly Guid ValidAvatarId = Guid.NewGuid();

    private static VirtualTryOnSession CreateSession(
        TryOnSessionType sessionType = TryOnSessionType.Model3D,
        Guid? avatarId = null)
        => VirtualTryOnSession.Create(
            ValidCustomerId,
            ValidProductId,
            ValidRetailerId,
            sessionType,
            avatarId);

    [Fact]
    public void Create_ValidParameters_SetsPropertiesCorrectly()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var retailerId = Guid.NewGuid();
        var avatarId = Guid.NewGuid();

        var session = VirtualTryOnSession.Create(
            customerId,
            productId,
            retailerId,
            TryOnSessionType.Overlay2D,
            avatarId);

        session.Id.Should().NotBeEmpty();
        session.CustomerId.Should().Be(customerId);
        session.ProductId.Should().Be(productId);
        session.RetailerId.Should().Be(retailerId);
        session.AvatarId.Should().Be(avatarId);
        session.SessionType.Should().Be(TryOnSessionType.Overlay2D);
        session.Status.Should().Be(SessionStatus.Processing);
        session.ResultImageUrl.Should().BeNull();
        session.RecommendedSize.Should().BeNull();
        session.ConfidenceScore.Should().BeNull();
        session.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void MarkAsCompleted_SetsStatusAndResultUrl()
    {
        var session = CreateSession(avatarId: ValidAvatarId);
        session.Status.Should().Be(SessionStatus.Processing);
        const string resultUrl = "https://cdn.example.com/tryon/result-abc123.jpg";

        session.MarkAsCompleted(resultUrl, "M", 0.92m, 4);

        session.Status.Should().Be(SessionStatus.Completed);
        session.ResultImageUrl.Should().Be(resultUrl);
        session.RecommendedSize.Should().Be("M");
        session.ConfidenceScore.Should().Be(0.92m);
        session.DurationSeconds.Should().Be(4);
    }

    [Fact]
    public void MarkAsFailed_SetsStatusAndError()
    {
        var session = CreateSession();
        session.Status.Should().Be(SessionStatus.Processing);

        session.MarkAsFailed();

        session.Status.Should().Be(SessionStatus.Failed);
        session.ResultImageUrl.Should().BeNull();
        session.RecommendedSize.Should().BeNull();
        session.ConfidenceScore.Should().BeNull();
    }
}