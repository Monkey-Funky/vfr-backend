using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Queries.GetSizeRecommendation;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Tests.Unit.Application.Features.Customer.Avatar;

public sealed class GetSizeRecommendationQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ISizeRecommendationService> _sizeServiceMock = new();
    private readonly GetSizeRecommendationQueryHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public GetSizeRecommendationQueryHandlerTests()
    {
        _sut = new GetSizeRecommendationQueryHandler(
            _contextMock.Object,
            _userServiceMock.Object,
            _sizeServiceMock.Object);

        _userServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static Domain.Entities.Customer.Avatar BuildAvatar(Guid? customerId = null)
        => Domain.Entities.Customer.Avatar.Create(
            customerId ?? CustomerId,
            heightCm: 178m,
            weightKg: 75m,
            chestCm: 98m,
            waistCm: 82m,
            hipsCm: 102m,
            shoulderWidthCm: 44m);

    private void SetupAvatarsDbSet(List<Domain.Entities.Customer.Avatar> avatars)
    {
        var mockSet = avatars.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Avatars).Returns(mockSet.Object);
    }

    [Fact]
    public async Task Handle_NoAvatar_ThrowsNotFoundException()
    {
        SetupAvatarsDbSet([]);

        var act = () => _sut.Handle(new GetSizeRecommendationQuery(ProductId), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*NO_AVATAR*");
    }

    [Fact]
    public async Task Handle_AvatarWithMeasurements_ReturnsRecommendation()
    {
        var avatar = BuildAvatar();
        SetupAvatarsDbSet([avatar]);

        var expectedRecommendation = new SizeRecommendationDto(
            ProductId, "M", 0.92m, "Best fit based on chest and waist measurements.");

        _sizeServiceMock
            .Setup(s => s.RecommendSizeAsync(avatar, ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRecommendation);

        var result = await _sut.Handle(new GetSizeRecommendationQuery(ProductId), CancellationToken.None);

        result.Should().NotBeNull();
        result.RecommendedSize.Should().Be("M");
        result.ConfidenceScore.Should().Be(0.92m);
        result.ProductId.Should().Be(ProductId);
    }

    [Fact]
    public async Task Handle_ServiceCallIsMadeWithCorrectMeasurements()
    {
        var avatar = BuildAvatar();
        SetupAvatarsDbSet([avatar]);

        _sizeServiceMock
            .Setup(s => s.RecommendSizeAsync(It.IsAny<Domain.Entities.Customer.Avatar>(), ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SizeRecommendationDto(ProductId, "L", 0.85m, "Justification."));

        await _sut.Handle(new GetSizeRecommendationQuery(ProductId), CancellationToken.None);

        _sizeServiceMock.Verify(
            s => s.RecommendSizeAsync(
                It.Is<Domain.Entities.Customer.Avatar>(a =>
                    a.CustomerId == CustomerId &&
                    a.HeightCm == 178m &&
                    a.WeightKg == 75m &&
                    a.ChestCm == 98m),
                ProductId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullCustomerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetSizeRecommendationQuery(ProductId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}