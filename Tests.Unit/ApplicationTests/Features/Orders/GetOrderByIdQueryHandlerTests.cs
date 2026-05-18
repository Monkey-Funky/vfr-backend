using Application.Features.Orders.DTOs;
using Application.Features.Orders.Queries.GetOrderById;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.ApplicationTests.Features.Orders;

public sealed class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetOrderByIdQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetOrderByIdQueryHandlerTests()
    {
        _sut = new GetOrderByIdQueryHandler(_orderRepoMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
    }

    private void SetupOrderLookup(Order? order) =>
        _orderRepoMock
            .Setup(x => x.GetByIdWithItemsAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

    private static Order CreateOrder(Guid retailerId, int itemCount = 1)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => ((Guid?)null, "Product", 100m, 1))
            .ToList()
            .AsReadOnly();
        return Order.Create(retailerId, Guid.NewGuid(), "Jane Doe", items);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        SetupOrderLookup(null);

        var act = () => _sut.Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OrderBelongsToDifferentRetailer_ThrowsForbiddenException()
    {
        _orderRepoMock
            .Setup(x => x.GetByIdWithItemsAsync(
                It.IsAny<Guid>(),
                RetailerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var act = () => _sut.Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidId_ReturnsMappedOrderDto()
    {
        var order = CreateOrder(RetailerId);
        SetupOrderLookup(order);

        var result = await _sut.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.RetailerId.Should().Be(order.RetailerId);
        result.CustomerName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task Handle_ValidId_IncludesOrderItems()
    {
        var order = CreateOrder(RetailerId, itemCount: 3);
        SetupOrderLookup(order);

        var result = await _sut.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.ItemCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ValidId_QueriesWithCorrectRetailerId()
    {
        var order = CreateOrder(RetailerId);
        SetupOrderLookup(order);
        var orderId = order.Id;

        await _sut.Handle(new GetOrderByIdQuery(orderId), CancellationToken.None);

        _orderRepoMock.Verify(
            x => x.GetByIdWithItemsAsync(orderId, RetailerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidId_ReturnsDtoWithCorrectStatus()
    {
        var order = CreateOrder(RetailerId);
        SetupOrderLookup(order);

        var result = await _sut.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.Status.Should().Be("NotProcessed");
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}