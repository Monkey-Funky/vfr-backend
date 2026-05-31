using Application.Interfaces.Services;
using Application.Features.Customer.Avatar.Queries.GetAvatar;
using Application.Interfaces.Persistence;
using Domain.Entities.Customer;

namespace Tests.Unit.Application.Features.Customer.Avatar;

public sealed class GetAvatarQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetAvatarQueryHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();

    public GetAvatarQueryHandlerTests()
    {
        _sut = new GetAvatarQueryHandler(_contextMock.Object, _cacheServiceMock.Object);
    }

    private static Domain.Entities.Customer.Avatar BuildAvatar(Guid? customerId = null)
        => Domain.Entities.Customer.Avatar.Create(
            customerId ?? CustomerId,
            heightCm: 175m,
            weightKg: 70m,
            chestCm: 95m,
            waistCm: 80m,
            hipsCm: 100m);

    private void SetupAvatarsDbSet(List<Domain.Entities.Customer.Avatar> avatars)
    {
        var mockSet = avatars.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Avatars).Returns(mockSet.Object);
    }

    [Fact]
    public async Task Handle_NoAvatar_ReturnsNull()
    {
        SetupAvatarsDbSet([]);

        var act = () => _sut.Handle(new GetAvatarQuery(CustomerId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AvatarExists_ReturnsMappedDto()
    {
        var avatar = BuildAvatar(CustomerId);
        SetupAvatarsDbSet([avatar]);

        var result = await _sut.Handle(new GetAvatarQuery(CustomerId), CancellationToken.None);

        result.Should().NotBeNull();
        result.HeightCm.Should().Be(175m);
        result.WeightKg.Should().Be(70m);
        result.ChestCm.Should().Be(95m);
        result.WaistCm.Should().Be(80m);
        result.HipsCm.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_AvatarBelongsToDifferentCustomer_ThrowsForbiddenException()
    {
        var avatarForOtherCustomer = BuildAvatar(OtherCustomerId);
        SetupAvatarsDbSet([avatarForOtherCustomer]);

        var act = () => _sut.Handle(new GetAvatarQuery(CustomerId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_MapsAllMeasurementFields()
    {
        var avatar = Domain.Entities.Customer.Avatar.Create(
            CustomerId, 180m, 80m,
            chestCm: 100m, waistCm: 85m, hipsCm: 105m,
            shoulderWidthCm: 45m, inseamCm: 82m, neckCm: 38m,
            armLengthCm: 62m, shoeSizeEu: 43m);

        SetupAvatarsDbSet([avatar]);

        var result = await _sut.Handle(new GetAvatarQuery(CustomerId), CancellationToken.None);

        result.HeightCm.Should().Be(180m);
        result.WeightKg.Should().Be(80m);
        result.ChestCm.Should().Be(100m);
        result.ShoulderWidthCm.Should().Be(45m);
        result.InseamCm.Should().Be(82m);
        result.NeckCm.Should().Be(38m);
        result.ArmLengthCm.Should().Be(62m);
        result.ShoeSizeEu.Should().Be(43m);
    }
}