using Application.Features.Offers.Commands.DeleteOffer;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;

namespace Tests.Unit.Application.Features.Offers;

public sealed class DeleteOfferCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Offer>> _offerRepoMock = new();
    private readonly DeleteOfferCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OfferId = Guid.NewGuid();

    public DeleteOfferCommandHandlerTests()
    {
        _sut = new DeleteOfferCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Offer>()).Returns(_offerRepoMock.Object);

        _offerRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static Domain.Entities.Retailer.Offer BuildActiveOffer()
        => Domain.Entities.Retailer.Offer.Create(
            retailerId: RetailerId,
            title: "Test Offer",
            description: null,
            offerType: OfferType.Product,
            productId: Guid.NewGuid(),
            categoryId: null,
            discountType: DiscountType.Percentage,
            discountValue: 10m,
            startDate: DateOnly.FromDateTime(DateTime.UtcNow),
            endDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            coverImageUrl: "https://cdn.example.com/image.jpg");

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        var offer = BuildActiveOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var result = await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_OfferNotFound_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Offer?)null);

        var act = () => _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OfferBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Offer?)null);

        var act = () => _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_SetsIsDeletedToTrue()
    {
        var offer = BuildActiveOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        Domain.Entities.Retailer.Offer? capturedOffer = null;
        _offerRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.Retailer.Offer, CancellationToken>((o, _) => capturedOffer = o)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        capturedOffer.Should().NotBeNull();
        capturedOffer!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_CallsUpdateAndSaveChanges()
    {
        var offer = BuildActiveOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        _offerRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulDelete_InvalidatesOffersCache()
    {
        var offer = BuildActiveOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(key => key.Contains(RetailerId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ExpiredOffer_SoftDeletesSuccessfully()
    {
        var offer = BuildActiveOffer();
        offer.Deactivate();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var result = await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        offer.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InactiveOffer_SoftDeletesSuccessfully()
    {
        var offer = BuildActiveOffer();
        offer.ToggleStatus();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var result = await _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        offer.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FailedSave_DoesNotInvalidateCache()
    {
        var offer = BuildActiveOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var act = () => _sut.Handle(new DeleteOfferCommand(OfferId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}