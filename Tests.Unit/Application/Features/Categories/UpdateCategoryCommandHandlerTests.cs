// tests/Tests.Unit/Application/Features/Categories/UpdateCategoryCommandHandlerTests.cs
using Application.Features.Categories.Commands.UpdateCategory;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Categories;

public sealed class UpdateCategoryCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Category>> _categoryRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly UpdateCategoryCommandHandler _handler;

    public UpdateCategoryCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _categoryRepoMock = MockRepository.Create<IRepository<Category>>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _fileStorageMock = MockRepository.Create<IFileStorageService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _unitOfWorkMock.Setup(u => u.Repository<Category>()).Returns(_categoryRepoMock.Object);

        _handler = new UpdateCategoryCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CategoryOwnedByAnotherRetailer_ThrowsNotFoundException()
    {
        // Arrange
        // The IDOR pattern in this codebase returns NotFoundException (not UnauthorizedException)
        // because the query filters c.RetailerId == currentRetailerId — a different
        // retailer's category is simply "not found" from the current retailer's perspective.

        Guid currentRetailerId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(currentRetailerId);

        // Repository returns null — the category exists but belongs to a different retailer
        _categoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        // FIX: The UpdateCategoryCommand record uses 'Status' not 'NewStatus'.
        //      Passing 'NewStatus' caused "does not have a parameter named 'NewStatus'" compile error.
        var command = new UpdateCategoryCommand(
            CategoryId: categoryId,
            NewName: "Hacked Name",
            NewDescription: null,
            ShouldUpdateDescription: false,
            NewCoverImageStream: null,
            NewCoverImageFileName: null,
            NewCoverImageContentType: null,
            Status: null);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Category*");
    }

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesCategoryAndInvalidatesCache()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var existingCategory = Category.Create(
            retailerId, "Old Name", "Old desc",
            "https://cdn.example.com/old.jpg", Category.CategoryStatus.Active);
        typeof(BaseEntity)
            .GetProperty("Id")!.SetValue(existingCategory, categoryId);

        _categoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCategory);

        _categoryRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
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

        // FIX: Correct parameter name is 'Status', not 'NewStatus'.
        var command = new UpdateCategoryCommand(
            CategoryId: categoryId,
            NewName: "Updated Electronics",
            NewDescription: "Updated description",
            ShouldUpdateDescription: true,
            NewCoverImageStream: null,
            NewCoverImageFileName: null,
            NewCoverImageContentType: null,
            Status: null);

        // Act
        Shared.DTOs.Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingCategory.Name.Should().Be("Updated Electronics");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}