// tests/Tests.Unit/Application/Features/Categories/GetCategoriesQueryHandlerTests.cs
using Application.Features.Categories.DTOs;
using Application.Features.Categories.Queries.GetCategories;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Categories;

public sealed class GetCategoriesQueryHandlerTests : TestBase
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly GetCategoriesQueryHandler _handler;

    public GetCategoriesQueryHandlerTests()
    {
        _contextMock = MockRepository.Create<IApplicationDbContext>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _handler = new GetCategoriesQueryHandler(
            _contextMock.Object,
            _currentUserMock.Object,
            _cacheMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Category BuildCategory(Guid retailerId, string name)
        => Category.Create(retailerId, name, null, "https://cdn.example.com/img.jpg",
            Category.CategoryStatus.Active);

    private void SetupCacheMiss()
    {
        _cacheMock
            .Setup(c => c.GetAsync<PagedResult<CategoryDto>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<CategoryDto>?)null);

        _cacheMock
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<PagedResult<CategoryDto>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_OnlyReturnsCategoriesBelongingToCallingRetailer_PreventsIDOR()
    {
        // Arrange
        Guid retailerA = Guid.NewGuid();
        Guid retailerB = Guid.NewGuid();

        var catA1 = BuildCategory(retailerA, "Electronics");
        var catA2 = BuildCategory(retailerA, "Fashion");
        var catB1 = BuildCategory(retailerB, "Intruder Category");

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerA);

        // DbSet contains categories from BOTH retailers
        var catDbSet = MockDbSetFactory.Create(new List<Category> { catA1, catA2, catB1 });
        _contextMock.Setup(c => c.Categories).Returns(catDbSet.Object);

        var subCatDbSet = MockDbSetFactory.Create(new List<SubCategory>());
        _contextMock.Setup(c => c.SubCategories).Returns(subCatDbSet.Object);

        SetupCacheMiss();

        var query = new GetCategoriesQuery(PageNumber: 1, PageSize: 20);

        // Act
        PagedResult<CategoryDto> result = await _handler.Handle(query, CancellationToken.None);

        // Assert — IDOR: only retailerA's categories are returned
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(dto =>
            dto.RetailerId.Should().Be(retailerA,
                "the handler must filter by the current retailer's ID from the JWT, never from the request"));

        result.Items.Should().NotContain(
            dto => dto.Name == "Intruder Category",
            "retailerB's category must never appear in retailerA's response");
    }

    [Fact]
    public async Task Handle_CacheMiss_QueriesDatabaseAndReturnsCorrectPagedResult()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        var categories = Enumerable.Range(1, 7)
            .Select(i => BuildCategory(retailerId, $"Category {i}"))
            .ToList();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var catDbSet = MockDbSetFactory.Create(categories);
        _contextMock.Setup(c => c.Categories).Returns(catDbSet.Object);

        var subCatDbSet = MockDbSetFactory.Create(new List<SubCategory>());
        _contextMock.Setup(c => c.SubCategories).Returns(subCatDbSet.Object);

        SetupCacheMiss();

        var query = new GetCategoriesQuery(PageNumber: 1, PageSize: 5);

        // Act
        PagedResult<CategoryDto> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(7);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(5);
        result.Items.Should().HaveCount(5);
    }
}