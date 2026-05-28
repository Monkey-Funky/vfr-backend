using Application.Features.Customer.Outfits.Queries.GetOutfits;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using MockQueryable.Moq;

namespace Tests.Unit.ApplicationTests.Features.Customer.Outfits;

public sealed class GetOutfitsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetOutfitsQueryHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();

    public GetOutfitsQueryHandlerTests()
    {
        _sut = new GetOutfitsQueryHandler(_contextMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static CustomerOutfit CreateOutfit(Guid? customerId = null, string name = "My Outfit")
        => CustomerOutfit.Create(customerId ?? CustomerId, name);

    private void SetupContext(List<CustomerOutfit> outfits, List<Product>? products = null)
    {
        var outfitsMock = outfits.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.CustomerOutfits).Returns(outfitsMock.Object);

        var productList = products ?? [];
        var productsMock = productList.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(c => c.Products).Returns(productsMock.Object);
    }

    [Fact]
    public async Task Handle_NoOutfits_ReturnsEmptyList()
    {
        SetupContext([]);

        var result = await _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OutfitsExist_ReturnsMappedList()
    {
        var outfit1 = CreateOutfit(name: "Casual Look");
        var outfit2 = CreateOutfit(name: "Formal Look");
        SetupContext([outfit1, outfit2]);

        var result = await _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.Should().Contain(x => x.Name == "Casual Look");
        result.Items.Should().Contain(x => x.Name == "Formal Look");
    }

    [Fact]
    public async Task Handle_CustomerSeesOnlyOwnOutfits()
    {
        var ownOutfit = CreateOutfit(CustomerId, "Mine");
        var otherOutfit = CreateOutfit(OtherCustomerId, "Theirs");
        SetupContext([ownOutfit, otherOutfit]);

        var result = await _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Mine");
    }

    [Fact]
    public async Task Handle_MissingCustomerId_ThrowsUnauthorizedAccessException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);
        SetupContext([]);

        var act = () => _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_OutfitWithItems_ReturnsCorrectItemCount()
    {
        var outfit = CreateOutfit();
        outfit.AddOrUpdateItem(Guid.NewGuid(), SlotType.Top, 1);
        outfit.AddOrUpdateItem(Guid.NewGuid(), SlotType.Bottom, 2);
        SetupContext([outfit]);

        var result = await _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MultipleOutfits_ReturnPagedResultWithConsistentCounts()
    {
        var outfits = Enumerable.Range(1, 4)
            .Select(i => CreateOutfit(CustomerId, $"Outfit {i}"))
            .ToList();

        SetupContext(outfits);

        var result = await _sut.Handle(new GetOutfitsQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(4);
        result.Items.Should().HaveCount(4);
        result.PageNumber.Should().Be(1);
    }
}