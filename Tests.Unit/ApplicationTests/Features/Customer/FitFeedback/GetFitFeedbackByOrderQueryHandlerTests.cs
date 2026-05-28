using Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByOrder;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit.ApplicationTests.Features.Customer.FitFeedback;

public sealed class GetFitFeedbackByOrderQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly GetFitFeedbackByOrderQueryHandler _sut;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OtherCustomerId = Guid.NewGuid();
    private static readonly Guid RetailerId = Guid.NewGuid();

    public GetFitFeedbackByOrderQueryHandlerTests()
    {
        _sut = new GetFitFeedbackByOrderQueryHandler(_contextMock.Object, _currentUserServiceMock.Object);
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns(CustomerId);
    }

    private static Order CreateOrder(Guid customerId, Guid? orderId = null)
    {
        var order = Order.Create(
            RetailerId,
            customerId,
            "Customer Name",
            new List<(Guid?, string, decimal, int)> { (Guid.NewGuid(), "Product A", 100m, 1) }.AsReadOnly());

        if (orderId.HasValue)
        {
            typeof(Domain.Common.BaseEntity)
                .GetProperty(nameof(Domain.Common.BaseEntity.Id))!
                .SetValue(order, orderId.Value);
        }

        return order;
    }

    private static OrderItem CreateOrderItem(Guid orderId, Guid? itemId = null)
    {
        var item = OrderItem.Create(orderId, Guid.NewGuid(), "Product A", 100m, 1);

        if (itemId.HasValue)
        {
            typeof(Domain.Common.BaseEntity)
                .GetProperty(nameof(Domain.Common.BaseEntity.Id))!
                .SetValue(item, itemId.Value);
        }

        return item;
    }

    private static Domain.Entities.Customer.FitFeedback CreateFeedback(Guid orderItemId, Guid customerId)
        => Domain.Entities.Customer.FitFeedback.Create(
            customerId,
            orderItemId,
            Guid.NewGuid(),
            3,
            "M",
            "L",
            "Runs small");

    private void SetupContext(
        List<Order> orders,
        List<OrderItem> orderItems,
        List<Domain.Entities.Customer.FitFeedback> feedbacks)
    {
        _contextMock.Setup(c => c.Orders).Returns(orders.AsQueryable().BuildMockDbSet().Object);
        _contextMock.Setup(c => c.OrderItems).Returns(orderItems.AsQueryable().BuildMockDbSet().Object);
        _contextMock.Setup(c => c.FitFeedback).Returns(feedbacks.AsQueryable().BuildMockDbSet().Object);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        var query = new GetFitFeedbackByOrderQuery(Guid.NewGuid());
        SetupContext([], [], []);

        var act = () => _sut.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OrderBelongsToOtherCustomer_ThrowsNotFoundException()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(OtherCustomerId, orderId);
        SetupContext([order], [], []);

        var act = () => _sut.Handle(new GetFitFeedbackByOrderQuery(orderId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_FeedbackExists_ReturnsMappedDto()
    {
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var order = CreateOrder(CustomerId, orderId);
        var orderItem = CreateOrderItem(orderId, itemId);
        var feedback = CreateFeedback(itemId, CustomerId);

        SetupContext([order], [orderItem], [feedback]);

        var result = await _sut.Handle(new GetFitFeedbackByOrderQuery(orderId), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Items[0].CustomerId.Should().Be(CustomerId);
        result.Items[0].OrderItemId.Should().Be(itemId);
        result.Items[0].FitRating.Should().Be(3);
    }

    [Fact]
    public async Task Handle_CustomerSeesOnlyOwnFeedback()
    {
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var otherOrderId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();

        var order = CreateOrder(CustomerId, orderId);
        var otherOrder = CreateOrder(OtherCustomerId, otherOrderId);
        var orderItem = CreateOrderItem(orderId, itemId);
        var otherOrderItem = CreateOrderItem(otherOrderId, otherItemId);
        var ownFeedback = CreateFeedback(itemId, CustomerId);
        var otherFeedback = CreateFeedback(otherItemId, OtherCustomerId);

        SetupContext([order, otherOrder], [orderItem, otherOrderItem], [ownFeedback, otherFeedback]);

        var result = await _sut.Handle(new GetFitFeedbackByOrderQuery(orderId), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items[0].CustomerId.Should().Be(CustomerId);
    }

    [Fact]
    public async Task Handle_NoFeedbackForOrder_ReturnsEmptyPagedResult()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(CustomerId, orderId);
        SetupContext([order], [], []);

        var result = await _sut.Handle(new GetFitFeedbackByOrderQuery(orderId), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingCustomerId_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.CustomerId).Returns((Guid?)null);
        SetupContext([], [], []);

        var act = () => _sut.Handle(new GetFitFeedbackByOrderQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_PaginationApplied_ReturnsCorrectPage()
    {
        var orderId = Guid.NewGuid();
        var order = CreateOrder(CustomerId, orderId);

        var items = Enumerable.Range(0, 5).Select(_ =>
        {
            var itemId = Guid.NewGuid();
            return CreateOrderItem(orderId, itemId);
        }).ToList();

        var feedbacks = items.Select(i => CreateFeedback(i.Id, CustomerId)).ToList();

        SetupContext([order], items, feedbacks);

        var result = await _sut.Handle(new GetFitFeedbackByOrderQuery(orderId, PageNumber: 1, PageSize: 3), CancellationToken.None);

        result.PageSize.Should().Be(3);
        result.PageNumber.Should().Be(1);
        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(3);
    }
}