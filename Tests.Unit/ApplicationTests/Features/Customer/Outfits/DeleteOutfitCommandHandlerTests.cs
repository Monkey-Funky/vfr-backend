using Application.Features.Customer.Outfits.Commands.DeleteOutfit;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Customer.Outfits;

public sealed class DeleteOutfitCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly DeleteOutfitCommandHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();

    public DeleteOutfitCommandHandlerTests()
    {
        _sut = new DeleteOutfitCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object);

        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static CustomerOutfit CreateOutfit(Guid customerId, Guid? outfitId = null)
    {
        var outfit = CustomerOutfit.Create(customerId, "Test Outfit", "Casual");
        if (outfitId.HasValue)
            typeof(Domain.Common.BaseEntity).GetProperty(nameof(Domain.Common.BaseEntity.Id))!.SetValue(outfit, outfitId.Value);
        return outfit;
    }

    [Fact]
    public async Task Handle_ValidRequest_SoftDeletesOutfit()
    {
        var outfitId = Guid.NewGuid();
        var outfit = CreateOutfit(CustomerId, outfitId);
        _contextMock.Setup(x => x.CustomerOutfits).ReturnsDbSet(new List<CustomerOutfit> { outfit });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new DeleteOutfitCommand(outfitId);
        await _sut.Handle(command, CancellationToken.None);

        outfit.IsDeleted.Should().BeTrue();
        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomerIdIsNull_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);
        var command = new DeleteOutfitCommand(Guid.NewGuid());

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*authenticated customers*");
    }

    [Fact]
    public async Task Handle_OutfitNotFound_ThrowsNotFoundException()
    {
        _contextMock.Setup(x => x.CustomerOutfits).ReturnsDbSet(new List<CustomerOutfit>());

        var command = new DeleteOutfitCommand(Guid.NewGuid());
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OutfitBelongsToDifferentCustomer_ThrowsUnauthorizedAccessException()
    {
        var outfitId = Guid.NewGuid();
        var anotherCustomerId = Guid.NewGuid();
        var outfit = CreateOutfit(anotherCustomerId, outfitId);
        _contextMock.Setup(x => x.CustomerOutfits).ReturnsDbSet(new List<CustomerOutfit> { outfit });

        var command = new DeleteOutfitCommand(outfitId);
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not authorized*");
    }

    [Fact]
    public async Task Handle_OutfitWithItems_SoftDeletesOutfitAndAllItems()
    {
        var outfitId = Guid.NewGuid();
        var outfit = CreateOutfit(CustomerId, outfitId);
        outfit.AddOrUpdateItem(Guid.NewGuid(), SlotType.Top, 1);
        outfit.AddOrUpdateItem(Guid.NewGuid(), SlotType.Bottom, 2);

        _contextMock.Setup(x => x.CustomerOutfits).ReturnsDbSet(new List<CustomerOutfit> { outfit });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new DeleteOutfitCommand(outfitId);
        await _sut.Handle(command, CancellationToken.None);

        outfit.IsDeleted.Should().BeTrue();
        outfit.Items.Should().AllSatisfy(i => i.IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_ValidRequest_SaveChangesCalledOnce()
    {
        var outfitId = Guid.NewGuid();
        var outfit = CreateOutfit(CustomerId, outfitId);
        _contextMock.Setup(x => x.CustomerOutfits).ReturnsDbSet(new List<CustomerOutfit> { outfit });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.Handle(new DeleteOutfitCommand(outfitId), CancellationToken.None);

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}