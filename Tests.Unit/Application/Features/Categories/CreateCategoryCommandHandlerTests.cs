// tests/Tests.Unit/Application/Features/Categories/CreateCategoryCommandHandlerTests.cs
using Application.Features.Categories.Commands.CreateCategory;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Categories;

public sealed class CreateCategoryCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Category>> _categoryRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly CreateCategoryCommandHandler _handler;

    public CreateCategoryCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _categoryRepoMock = MockRepository.Create<IRepository<Category>>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _fileStorageMock = MockRepository.Create<IFileStorageService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _unitOfWorkMock.Setup(u => u.Repository<Category>()).Returns(_categoryRepoMock.Object);

        _handler = new CreateCategoryCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateCategoryName_ThrowsConflictException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        _categoryRepoMock
            .Setup(r => r.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // name already exists

        using var stream = new MemoryStream(new byte[100]);
        var command = new CreateCategoryCommand(
            "Electronics", null, stream,
            "cover.jpg", "image/jpeg",
            Category.CategoryStatus.Active);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Name*");
    }

    [Fact]
    public async Task Handle_ValidNewCategory_CreatesEntityAndInvalidatesCache()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        _categoryRepoMock
            .Setup(r => r.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // name does not exist

        _fileStorageMock
            .Setup(f => f.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.example.com/category-covers/abc.jpg")
            .Verifiable();

        Category? savedCategory = null;

        // FIX: IRepository<T>.AddAsync returns Task<T>, not Task.
        //      The Callback and Returns are merged into a single Returns<T, CancellationToken>
        //      delegate so the mock return type matches Task<Category> exactly.
        _categoryRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns<Category, CancellationToken>((category, _) =>
            {
                savedCategory = category;
                return Task.FromResult(category);
            })
            .Verifiable();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        _cacheMock
            .Setup(c => c.RemoveByPrefixAsync(
                It.Is<string>(k => k.Contains(retailerId.ToString())),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var stream = new MemoryStream(new byte[200]);
        var command = new CreateCategoryCommand(
            "Electronics", "All electronics", stream,
            "cover.jpg", "image/jpeg",
            Category.CategoryStatus.Active);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        savedCategory.Should().NotBeNull();
        savedCategory!.Name.Should().Be("Electronics");
        savedCategory.RetailerId.Should().Be(retailerId);
        savedCategory.IsDeleted.Should().BeFalse();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}