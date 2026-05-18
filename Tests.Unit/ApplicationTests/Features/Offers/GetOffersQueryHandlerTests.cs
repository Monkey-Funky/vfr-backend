using Application.Features.Offers.DTOs;
using Application.Features.Offers.Queries.GetOffers;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using Shared.DTOs;

namespace Tests.Unit.ApplicationTests.Features.Offers;

public sealed class GetOffersQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetOffersQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetOffersQueryHandlerTests()
    {
        _sut = new GetOffersQueryHandler(_contextMock.Object, _currentUserServiceMock.Object, _cacheServiceMock.Object);
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _cacheServiceMock
            .Setup(x => x.GetAsync<PagedResult<OfferDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<OfferDto>?)null);

        _cacheServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<PagedResult<OfferDto>>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Domain.Entities.Retailer.Offer CreateOffer(Guid retailerId, string status = OfferStatus.Active)
    {
        var offer = Domain.Entities.Retailer.Offer.Create(
            retailerId, "Summer Sale", null, OfferType.Category,
            null, Guid.NewGuid(), DiscountType.Percentage, 20m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            "https://cdn.example.com/cover.jpg");

        if (status == OfferStatus.Inactive)
            offer.ToggleStatus();
        else if (status == OfferStatus.Expired)
            offer.Deactivate();

        return offer;
    }

    private void SetupOffersDbSet(IList<Domain.Entities.Retailer.Offer> offers) =>
        _contextMock.Setup(x => x.Offers).Returns(offers.AsQueryable().BuildMockDbSet().Object);

    [Fact]
    public async Task Handle_NoOffers_ReturnsEmptyList()
    {
        SetupOffersDbSet(new List<Domain.Entities.Retailer.Offer>());

        var result = await _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OffersExist_ReturnsFilteredList()
    {
        SetupOffersDbSet(new List<Domain.Entities.Retailer.Offer>
        {
            CreateOffer(RetailerId),
            CreateOffer(RetailerId)
        });

        var result = await _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FilterByActiveStatus_ReturnsOnlyActiveOffers()
    {
        SetupOffersDbSet(new List<Domain.Entities.Retailer.Offer>
        {
            CreateOffer(RetailerId, OfferStatus.Active),
            CreateOffer(RetailerId, OfferStatus.Inactive)
        });

        var result = await _sut.Handle(new GetOffersQuery(Status: OfferStatus.Active), CancellationToken.None);

        result.Items.Should().OnlyContain(o => o.Status == OfferStatus.Active);
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnOffers()
    {
        var ownOffer = CreateOffer(RetailerId);
        var otherOffer = CreateOffer(OtherRetailerId);
        SetupOffersDbSet(new List<Domain.Entities.Retailer.Offer> { ownOffer, otherOffer });

        var result = await _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        result.Items.Should().OnlyContain(o => o.Id == ownOffer.Id);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResultWithoutQueryingDatabase()
    {
        var cachedResult = new PagedResult<OfferDto>
        {
            Items = new List<OfferDto>().AsReadOnly(),
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<PagedResult<OfferDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var result = await _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        result.Should().BeSameAs(cachedResult);
        _contextMock.Verify(x => x.Offers, Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_StoresResultInCache()
    {
        SetupOffersDbSet(new List<Domain.Entities.Retailer.Offer> { CreateOffer(RetailerId) });

        await _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<PagedResult<OfferDto>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetOffersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_PaginationApplied_ReturnsCorrectPageMetadata()
    {
        var offers = Enumerable.Range(0, 15)
            .Select(_ => CreateOffer(RetailerId))
            .ToList();
        SetupOffersDbSet(offers);

        var result = await _sut.Handle(new GetOffersQuery(PageNumber: 2, PageSize: 5), CancellationToken.None);

        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
    }
}