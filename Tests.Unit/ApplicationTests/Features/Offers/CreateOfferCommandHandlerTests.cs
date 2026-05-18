using Application.Common;
using Application.Features.Offers.Commands.CreateOffer;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using Domain.Enums.Product;

namespace Tests.Unit.Application.Features.Offers;

public sealed class CreateOfferCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Offer>> _offerRepoMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Product>> _productRepoMock = new();
    private readonly Mock<IRepository<Domain.Entities.Retailer.Category>> _categoryRepoMock = new();
    private readonly CreateOfferCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();
    private const string UploadedImageUrl = "https://cdn.example.com/offers/image.jpg";

    public CreateOfferCommandHandlerTests()
    {
        _sut = new CreateOfferCommandHandler(
            _uowMock.Object,
            _userServiceMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Offer>()).Returns(_offerRepoMock.Object);
        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Product>()).Returns(_productRepoMock.Object);
        _uowMock.Setup(x => x.Repository<Domain.Entities.Retailer.Category>()).Returns(_categoryRepoMock.Object);

        _offerRepoMock
            .Setup(x => x.AddAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Offer o, CancellationToken _) => o);

        _uowMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _fileStorageMock
            .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadedImageUrl);
    }

    private static FileUploadDto BuildFileUploadDto()
        => new(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "cover.jpg", "image/jpeg", 3);

    private static Domain.Entities.Retailer.Product BuildActiveProduct(decimal price = 100m)
        => Domain.Entities.Retailer.Product.Create(RetailerId, "Test Product", price: price, status: ProductStatus.Active);

    private static Domain.Entities.Retailer.Category BuildCategory()
        => Domain.Entities.Retailer.Category.Create(RetailerId, "Electronics", null, "icon.jpg", "Active");

    private CreateOfferCommand BuildProductOfferCommand(
        string discountType = DiscountType.Percentage,
        decimal discountValue = 10m)
        => new(
            Title: "Summer Sale",
            Description: "10% off",
            OfferType: OfferType.Product,
            ProductId: ProductId,
            CategoryId: null,
            DiscountType: discountType,
            DiscountValue: discountValue,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CoverImage: BuildFileUploadDto());

    private CreateOfferCommand BuildCategoryOfferCommand()
        => new(
            Title: "Category Promo",
            Description: null,
            OfferType: OfferType.Category,
            ProductId: null,
            CategoryId: CategoryId,
            DiscountType: DiscountType.Percentage,
            DiscountValue: 15m,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: null,
            CoverImage: BuildFileUploadDto());

    [Fact]
    public async Task Handle_ValidProductOffer_ReturnsOfferIdSuccess()
    {
        var product = BuildActiveProduct();
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_ValidCategoryOffer_ReturnsOfferIdSuccess()
    {
        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.Handle(BuildCategoryOfferCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ProductNotFound_ThrowsNotFoundException()
    {
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.Retailer.Product?)null);

        var act = () => _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ProductIsNotActive_ThrowsBusinessRuleException()
    {
        var draftProduct = Domain.Entities.Retailer.Product.Create(RetailerId, "Draft Product", status: ProductStatus.Draft);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftProduct);

        var act = () => _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PRODUCT_NOT_ACTIVE");
    }

    [Fact]
    public async Task Handle_InactiveProduct_ThrowsBusinessRuleException()
    {
        var inactiveProduct = Domain.Entities.Retailer.Product.Create(RetailerId, "Inactive Product", status: ProductStatus.Inactive);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveProduct);

        var act = () => _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("PRODUCT_NOT_ACTIVE");
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        _categoryRepoMock
            .Setup(x => x.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _sut.Handle(BuildCategoryOfferCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FixedDiscountExceedsProductPrice_ThrowsBusinessRuleException()
    {
        var product = BuildActiveProduct(price: 50m);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildProductOfferCommand(discountType: DiscountType.Fixed, discountValue: 75m);

        var act = () => _sut.Handle(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("DISCOUNT_EXCEEDS_PRICE");
    }

    [Fact]
    public async Task Handle_FixedDiscountEqualsProductPrice_ReturnsSuccess()
    {
        var product = BuildActiveProduct(price: 50m);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildProductOfferCommand(discountType: DiscountType.Fixed, discountValue: 50m);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PercentageDiscountDoesNotValidateProductPrice()
    {
        var product = BuildActiveProduct(price: 10m);
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = BuildProductOfferCommand(discountType: DiscountType.Percentage, discountValue: 90m);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuccessfulCreation_UploadsCoverImage()
    {
        var product = BuildActiveProduct();
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        _fileStorageMock.Verify(
            x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.Is<string>(folder => folder.Contains(RetailerId.ToString("N"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulCreation_PersistsOfferEntity()
    {
        var product = BuildActiveProduct();
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        _offerRepoMock.Verify(
            x => x.AddAsync(It.IsAny<Domain.Entities.Retailer.Offer>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulCreation_InvalidatesOffersCache()
    {
        var product = BuildActiveProduct();
        _productRepoMock
            .Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.Retailer.Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _sut.Handle(BuildProductOfferCommand(), CancellationToken.None);

        _cacheMock.Verify(
            x => x.RemoveByPrefixAsync(
                It.Is<string>(key => key.Contains(RetailerId.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}