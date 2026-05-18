using Application.Features.Customer.Wardrobe.Commands.CreateCollection;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Wardrobe;

public sealed class CreateCollectionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly CreateCollectionCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public CreateCollectionCommandHandlerTests()
    {
        _sut = new CreateCollectionCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsNewCollectionId()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _sut.Handle(new CreateCollectionCommand("Summer Wardrobe"), CancellationToken.None);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsCollectionToContext()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new CreateCollectionCommand("Winter Wardrobe"), CancellationToken.None);

        _contextMock.Verify(x => x.WardrobeCollections.Add(It.Is<WardrobeCollection>(c => c.CustomerId == CustomerId && c.Name == "Winter Wardrobe")), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new CreateCollectionCommand("Test"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*authenticated customers*");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsSaveChangesOnce()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new CreateCollectionCommand("Casual"), CancellationToken.None);

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateKeyViolation_ThrowsConflictException()
    {
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());

        var pgException = new Npgsql.PostgresException("duplicate key", "ERROR", "ERROR", "23505");
        var dbUpdateException = new DbUpdateException("Conflict", pgException);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbUpdateException);

        var act = () => _sut.Handle(new CreateCollectionCommand("Summer"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_NameWithLeadingAndTrailingSpaces_TrimsName()
    {
        WardrobeCollection? captured = null;
        _contextMock.Setup(x => x.WardrobeCollections).ReturnsDbSet(new List<WardrobeCollection>());
        _contextMock.Setup(x => x.WardrobeCollections.Add(It.IsAny<WardrobeCollection>()))
            .Callback<WardrobeCollection>(c => captured = c);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new CreateCollectionCommand("  My Collection  "), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Name.Should().Be("My Collection");
    }
}