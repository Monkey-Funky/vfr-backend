using Application.Features.Customer.Wardrobe.Commands.RenameCollection;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Wardrobe;

public sealed class RenameCollectionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly RenameCollectionCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public RenameCollectionCommandHandlerTests()
    {
        _sut = new RenameCollectionCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static WardrobeCollection CreateCollection(Guid customerId, Guid collectionId, string name = "Original Name")
    {
        var col = WardrobeCollection.Create(customerId, name);
        typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(col, collectionId);
        return col;
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new RenameCollectionCommand(Guid.NewGuid(), "New Name"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*authenticated customers*");
    }

    [Fact]
    public async Task Handle_CollectionNotFound_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());

        var act = () => _sut.Handle(new RenameCollectionCommand(Guid.NewGuid(), "New Name"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CollectionBelongsToDifferentCustomer_ThrowsNotFoundException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(Guid.NewGuid(), collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });

        var act = () => _sut.Handle(new RenameCollectionCommand(collectionId, "New Name"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidRequest_RenamesCollection()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new RenameCollectionCommand(collectionId, "Renamed Collection"), CancellationToken.None);

        collection.Name.Should().Be("Renamed Collection");
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateKeyViolation_ThrowsConflictException()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });

        var pgException = new Npgsql.PostgresException("duplicate key", "ERROR", "ERROR", "23505");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Conflict", pgException));

        var act = () => _sut.Handle(new RenameCollectionCommand(collectionId, "Existing Name"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_NameWithWhitespace_TrimsName()
    {
        var collectionId = Guid.NewGuid();
        var collection = CreateCollection(CustomerId, collectionId);
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection> { collection });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new RenameCollectionCommand(collectionId, "  Trimmed Name  "), CancellationToken.None);

        collection.Name.Should().Be("Trimmed Name");
    }
}