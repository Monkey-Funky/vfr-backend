// tests/Tests.Unit/Application/Features/Categories/DeleteCategoryCommandHandlerTests.cs
using Application.Features.Categories.Commands.DeleteCategory;
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

public sealed class DeleteCategoryCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Category>> _categoryRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly DeleteCategoryCommandHandler _handler;

    public DeleteCategoryCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _categoryRepoMock = MockRepository.Create<IRepository<Category>>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _contextMock = MockRepository.Create<IApplicationDbContext>();
        _fileStorageMock = MockRepository.Create<IFileStorageService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _unitOfWorkMock.Setup(u => u.Repository<Category>()).Returns(_categoryRepoMock.Object);

        _handler = new DeleteCategoryCommandHandler(
            _unitOfWorkMock.Object,
            _contextMock.Object,
            _currentUserMock.Object,
            _fileStorageMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        Guid retailerId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        _categoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var command = new DeleteCategoryCommand(categoryId);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCategory_ExecutesTransactionAndInvalidatesCacheAndDeletesS3Image()
    {
        Guid retailerId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        const string imgUrl = "https://cdn.example.com/cat-img.jpg";

        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var category = Category.Create(retailerId, "Electronics", null, imgUrl,
            Category.CategoryStatus.Active);
        typeof(BaseEntity).GetProperty("Id")!.SetValue(category, categoryId);

        _categoryRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Category, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        _unitOfWorkMock
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<CancellationToken, Task>, CancellationToken>(
                (action, ct) => action(ct).GetAwaiter().GetResult())
            .Returns(Task.CompletedTask)
            .Verifiable();

        var emptyOfferDbSet = MockDbSetFactory.Create(new List<Offer>());
        _contextMock.Setup(c => c.Offers).Returns(emptyOfferDbSet.Object);

        var emptyProductDbSet = MockDbSetFactory.Create(new List<Product>());
        _contextMock.Setup(c => c.Products).Returns(emptyProductDbSet.Object);

        var emptySubCatDbSet = MockDbSetFactory.Create(new List<SubCategory>());
        _contextMock.Setup(c => c.SubCategories).Returns(emptySubCatDbSet.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        _fileStorageMock
            .Setup(f => f.DeleteAsync(imgUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _cacheMock
            .Setup(c => c.RemoveByPrefixAsync(
                It.Is<string>(k => k.Contains(retailerId.ToString())),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var command = new DeleteCategoryCommand(categoryId);

        Shared.DTOs.Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _unitOfWorkMock.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _fileStorageMock.Verify(
            f => f.DeleteAsync(imgUrl, It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheMock.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}