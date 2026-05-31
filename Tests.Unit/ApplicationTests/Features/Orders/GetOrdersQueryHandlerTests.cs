using Application.Features.Orders.DTOs;
using Application.Features.Orders.Queries.GetOrders;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Tests.Unit.ApplicationTests.Features.Orders;

public sealed class GetOrdersQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly GetOrdersQueryHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OtherRetailerId = Guid.NewGuid();

    public GetOrdersQueryHandlerTests()
    {
        _sut = new GetOrdersQueryHandler(_orderRepoMock.Object, _currentUserServiceMock.Object, _cacheServiceMock.Object);
        // Cache miss for all GetAsync calls — Moq returns Task<T?> default (null)
        // which simulates a cache miss so the handler always exercises the DB path.
        // RemoveAsync / RemoveByPrefixAsync are stubbed to complete successfully.
        _cacheServiceMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);
    }

    private void SetupPagedOrders(IReadOnlyList<Order> orders, int totalCount) =>
        _orderRepoMock
            .Setup(x => x.GetPagedOrdersAsync(
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((orders, totalCount));

    private static Order CreateOrder(Guid retailerId, string status = "NotProcessed") =>
        Order.Create(
            retailerId,
            Guid.NewGuid(),
            "John Doe",
            new List<(Guid?, string, decimal, int)> { (null, "Product A", 100m, 1) }.AsReadOnly());

    [Fact]
    public async Task Handle_NoOrders_ReturnsEmptyPagedResult()
    {
        SetupPagedOrders(new List<Order>().AsReadOnly(), 0);

        var result = await _sut.Handle(new GetOrdersQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OrdersExist_ReturnsPagedList()
    {
        var orders = new List<Order> { CreateOrder(RetailerId), CreateOrder(RetailerId) };
        SetupPagedOrders(orders.AsReadOnly(), 2);

        var result = await _sut.Handle(new GetOrdersQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsCorrectOrders()
    {
        var orders = new List<Order> { CreateOrder(RetailerId) };
        SetupPagedOrders(orders.AsReadOnly(), 1);

        var result = await _sut.Handle(new GetOrdersQuery(Status: "Processing"), CancellationToken.None);

        _orderRepoMock.Verify(x => x.GetPagedOrdersAsync(
            RetailerId,
            "Processing",
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PaginationApplied_ReturnsCorrectPage()
    {
        var orders = new List<Order> { CreateOrder(RetailerId) };
        SetupPagedOrders(orders.AsReadOnly(), 50);

        var result = await _sut.Handle(new GetOrdersQuery(PageNumber: 3, PageSize: 10), CancellationToken.None);

        _orderRepoMock.Verify(x => x.GetPagedOrdersAsync(
            It.IsAny<Guid>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            3,
            10,
            It.IsAny<CancellationToken>()), Times.Once);

        result.PageNumber.Should().Be(3);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(50);
    }

    [Fact]
    public async Task Handle_RetailerSeesOnlyOwnOrders()
    {
        SetupPagedOrders(new List<Order>().AsReadOnly(), 0);

        await _sut.Handle(new GetOrdersQuery(), CancellationToken.None);

        _orderRepoMock.Verify(x => x.GetPagedOrdersAsync(
            RetailerId,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(new GetOrdersQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_OrdersExist_ReturnsMappedDtosWithCorrectFields()
    {
        var order = CreateOrder(RetailerId);
        SetupPagedOrders(new List<Order> { order }.AsReadOnly(), 1);

        var result = await _sut.Handle(new GetOrdersQuery(), CancellationToken.None);

        var dto = result.Items[0];
        dto.RetailerId.Should().Be(order.RetailerId);
        dto.Status.Should().Be(order.Status);
        dto.OrderId.Should().Be(order.Id);
    }
}