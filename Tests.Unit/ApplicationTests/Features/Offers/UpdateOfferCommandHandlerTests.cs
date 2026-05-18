using Application.Common;
using Application.Features.Offers.Commands.UpdateOffer;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using Domain.Enums.Product;

namespace Tests.Unit.Application.Features.Offers;

public sealed class UpdateOfferCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Offer>> _offerRepoMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Product>> _productRepoMock = new();
    private readonly UpdateOfferCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OfferId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private const string UploadedImageUrl = "https://cdn.example.com/offers/new-image.jpg";

    public UpdateOfferCommandHandlerTests()
    {
        _sut = new UpdateOfferCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Offer>()).Returns(_offerRepoMock.Object);
        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Product>()).Returns(_productRepoMock.Object);

        _offerRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _fileStorageMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadedImageUrl);
    }

    private static Domain.Entities.Retailer.Offer BuildActiveProductOffer()
        => Domain.Entities.Retailer.Offer.Create(
            retailerId: RetailerId,
            title: "Original Title",
            description: null,
            offerType: OfferType.Product,
            productId: ProductId,
            categoryId: null,
            discountType: DiscountType.Percentage,
            discountValue: 10m,
            startDate: DateOnly.FromDateTime(DateTime.UtcNow),
            endDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            coverImageUrl: "https://cdn.example.com/original.jpg");

    private static Domain.Entities.Retailer.Offer BuildActiveCategoryOffer(Guid? categoryId = null)
        => Domain.Entities.Retailer.Offer.Create(
            retailerId: RetailerId,
            title: "Category Offer",
            description: null,
            offerType: OfferType.Category,
            productId: null,
            categoryId: categoryId ?? Guid.NewGuid(),
            discountType: DiscountType.Percentage,
            discountValue: 15m,
            startDate: DateOnly.FromDateTime(DateTime.UtcNow),
            endDate: null,
            coverImageUrl: "https://cdn.example.com/original.jpg");

    private static Domain.Entities.Retailer.Product BuildActiveProduct(decimal price = 100m)
        => Domain.Entities.Retailer.Product.Create(RetailerId, "Test Product", price: price, status: ProductStatus.Active);

    private static FileUploadDto BuildFileUploadDto()
        => new(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "new-cover.jpg", "image/jpeg", 3);

    private static UpdateOfferCommand BuildCommand(
        string discountType = DiscountType.Percentage,
        decimal discountValue = 20m,
        FileUploadDto? coverImage = null,
        string status = OfferStatus.Active)
        => new(
            OfferId: OfferId,
            Title: "Updated Title",
            Description: "Updated description",
            DiscountType: discountType,
            DiscountValue: discountValue,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            Status: status,
            CoverImage: coverImage);

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccess()
    {
        var offer = BuildActiveProductOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var product = BuildActiveProduct();
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _sut.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_OfferNotFound_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Offer?)null);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OfferBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Offer?)null);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FixedDiscountExceedsProductPrice_ThrowsBusinessRuleException()
    {
        var offer = BuildActiveProductOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var product = BuildActiveProduct(price: 30m);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand(discountType: DiscountType.Fixed, discountValue: 50m);

        var act = () => _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("DISCOUNT_EXCEEDS_PRICE");
    }

    [Fact]
    public async Task Handle_ExpiredOffer_ThrowsBusinessRuleException()
    {
        var offer = BuildActiveProductOffer();
        offer.Deactivate();

        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var act = () => _sut.Handle(BuildCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("OFFER_EXPIRED");
    }

    [Fact]
    public async Task Handle_WithNewCoverImage_UploadsNewImage()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = BuildCommand(coverImage: BuildFileUploadDto());
        await _sut.Handle(command, CancellationToken.None);

        _fileStorageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.Is<string>(folder => folder.Contains(RetailerId.ToString("N"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutNewCoverImage_DoesNotUploadImage()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = BuildCommand(coverImage: null);
        await _sut.Handle(command, CancellationToken.None);

        _fileStorageMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_StatusSetToExpiredManually_ThrowsBusinessRuleException()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = BuildCommand(status: OfferStatus.Expired);

        var act = () => _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("CANNOT_SET_EXPIRED");
    }

    [Fact]
    public async Task Handle_SuccessfulUpdate_CallsUpdateAndSaveChanges()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _offerRepoMock.Verify(
            x => x.UpdateAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulUpdate_InvalidatesOffersCache()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        await _sut.Handle(BuildCommand(), CancellationToken.None);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(key => key.Contains(RetailerId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_FixedDiscountBelowProductPrice_ReturnsSuccess()
    {
        var offer = BuildActiveProductOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var product = BuildActiveProduct(price: 100m);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildCommand(discountType: DiscountType.Fixed, discountValue: 50m);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CategoryOfferWithFixedDiscount_SkipsProductPriceValidation()
    {
        var offer = BuildActiveCategoryOffer();
        _offerRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Offer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var command = BuildCommand(discountType: DiscountType.Fixed, discountValue: 999m);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productRepoMock.Verify(
            x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}