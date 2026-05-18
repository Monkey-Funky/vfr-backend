using Application.Features.Categories.DTOs;
using Application.Features.Categories.Queries.GetCategories;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs;

namespace Tests.Unit.Application.Features.Categories;

public sealed class GetCategoriesQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly GetCategoriesQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetCategoriesQueryHandlerTests()
    {
        _sut = new GetCategoriesQueryHandler(
            _contextMock.Object,
            _userServiceMock.Object,
            _cacheMock.Object);

        _userServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _cacheMock
            .Setup(x => x.GetAsync<PagedResult<CategoryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<CategoryDto>?)null);

        _cacheMock
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<PagedResult<CategoryDto>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static Category BuildCategory(Guid? retailerId = null, string? status = null)
        => Category.Create(
            retailerId ?? RetailerId,
            $"Category-{Guid.NewGuid():N}",
            null,
            "https://example.com/cover.jpg",
            status ?? Category.CategoryStatus.Active);

    private void SetupDbSets(List<Category> categories, List<SubCategory>? subCategories = null)
    {
        var categoryMock = categories.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Categories).Returns(categoryMock.Object);

        var subCatList = subCategories ?? [];
        var subCatMock = subCatList.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.SubCategories).Returns(subCatMock.Object);
    }

    [Fact]
    public async Task Handle_NoCategories_ReturnsEmptyList()
    {
        SetupDbSets([]);

        var result = await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CategoriesExist_ReturnsMappedList()
    {
        var cat1 = BuildCategory();
        var cat2 = BuildCategory();
        SetupDbSets([cat1, cat2]);

        var result = await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().AllSatisfy(dto =>
        {
            dto.Id.Should().NotBeEmpty();
            dto.RetailerId.Should().Be(RetailerId);
        });
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnCategories()
    {
        var ownCategory = BuildCategory(retailerId: RetailerId);
        var otherCategory = BuildCategory(retailerId: OtherRetailerId);
        SetupDbSets([ownCategory, otherCategory]);

        var result = await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].RetailerId.Should().Be(RetailerId);
    }

    [Fact]
    public async Task Handle_NullRetailerId_ThrowsUnauthorizedException()
    {
        _userServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetCategoriesQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_CacheHit_DoesNotQueryDatabase()
    {
        var cached = new PagedResult<CategoryDto>
        {
            Items = [],
            TotalCount = 0,
            PageNumber = 1,
            PageSize = 20
        };

        _cacheMock
            .Setup(x => x.GetAsync<PagedResult<CategoryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var result = await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        result.Should().BeSameAs(cached);
        _contextMock.Verify(c => c.Categories, Times.Never);
    }

    [Fact]
    public async Task Handle_CacheMiss_StoresResultInCache()
    {
        SetupDbSets([BuildCategory()]);

        await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<PagedResult<CategoryDto>>(),
            TimeSpan.FromMinutes(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ResultIncludesSubCategoryCount()
    {
        var category = BuildCategory();
        var subCat = SubCategory.Create(category.Id, category.RetailerId, "Sub1", "Active");
        SetupDbSets([category], [subCat]);

        var result = await _sut.Handle(new GetCategoriesQuery(1, 20), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].SubCategoryCount.Should().Be(1);
    }
}