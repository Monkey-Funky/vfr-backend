using Application.Features.Offers.Commands.ToggleOfferStatus;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using System.Linq.Expressions;

namespace Tests.Unit.Application.Features.Offers;

public sealed class ToggleOfferStatusCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Offer>> _offerRepoMock = new();
    private readonly ToggleOfferStatusCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();

    public ToggleOfferStatusCommandHandlerTests()
    {
        _uowMock.Setup(x => x.Repository<Offer>()).Returns(_offerRepoMock.Object);
        _userMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _sut = new ToggleOfferStatusCommandHandler(
            _uowMock.Object,
            _userMock.Object,
            _cacheMock.Object);
    }

    private static Offer CreateActiveOffer()
    {
        return Offer.Create(
            RetailerId,
            "Test Offer",
            null,
            OfferType.Product,
            Guid.NewGuid(),
            null,
            DiscountType.Percentage,
            10m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            "https://cdn.test/cover.jpg");
    }

    private static Offer CreateInactiveOffer()
    {
        var offer = CreateActiveOffer();
        offer.ToggleStatus();
        return offer;
    }

    private static Offer CreateExpiredOffer()
    {
        var offer = CreateActiveOffer();
        offer.Deactivate();
        return offer;
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _userMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);
        var command = new ToggleOfferStatusCommand(Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_OfferNotFound_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var command = new ToggleOfferStatusCommand(Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OfferBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var command = new ToggleOfferStatusCommand(Guid.NewGuid());

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ExpiredOffer_ThrowsBusinessRuleException()
    {
        var expiredOffer = CreateExpiredOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredOffer);

        var command = new ToggleOfferStatusCommand(expiredOffer.Id);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("OFFER_EXPIRED");
    }

    [Fact]
    public async Task Handle_ActiveOffer_TogglesStatusToInactiveAndReturnsSuccess()
    {
        var activeOffer = CreateActiveOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeOffer);

        var command = new ToggleOfferStatusCommand(activeOffer.Id);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        activeOffer.Status.Should().Be(OfferStatus.Inactive);
    }

    [Fact]
    public async Task Handle_InactiveOffer_TogglesStatusToActiveAndReturnsSuccess()
    {
        var inactiveOffer = CreateInactiveOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveOffer);

        var command = new ToggleOfferStatusCommand(inactiveOffer.Id);

        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        inactiveOffer.Status.Should().Be(OfferStatus.Active);
    }

    [Fact]
    public async Task Handle_Success_CallsUpdateAsync()
    {
        var offer = CreateActiveOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = new ToggleOfferStatusCommand(offer.Id);

        await _sut.Handle(command, default);

        _offerRepoMock.Verify(
            x => x.UpdateAsync(It.Is<Offer>(o => o.Id == offer.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Success_SavesChanges()
    {
        var offer = CreateActiveOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = new ToggleOfferStatusCommand(offer.Id);

        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_InvalidatesCacheByRetailerPrefix()
    {
        var offer = CreateActiveOffer();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<Expression<Func<Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = new ToggleOfferStatusCommand(offer.Id);

        await _sut.Handle(command, default);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(s => s.StartsWith($"offers:{RetailerId}:")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}