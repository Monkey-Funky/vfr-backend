using Application.Features.Orders.Commands.UpdateOrderStatus;
using Application.Interfaces.Persistence;
using Domain.Enums.Orders;
using Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Features.Orders;

public sealed class UpdateOrderStatusCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ILogger<UpdateOrderStatusCommandHandler>> _loggerMock = new();
    private readonly UpdateOrderStatusCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public UpdateOrderStatusCommandHandlerTests()
    {
        _sut = new UpdateOrderStatusCommandHandler(
            _uowMock.Object,
            _contextMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);

        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private Order BuildOrder(string status = OrderStatus.NotProcessed, Guid? retailerId = null)
    {
        var items = new List<(Guid? ProductId, string ProductName, decimal UnitPrice, int Quantity)>
        {
            (Guid.NewGuid(), "Product A", 100m, 1)
        };

        var order = Order.Create(
            retailerId ?? RetailerId,
            Guid.NewGuid(),
            "Test Customer",
            items);

        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, OrderId);

        if (status != OrderStatus.NotProcessed)
        {
            typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
        }

        return order;
    }

    /// <summary>
    /// Sets up IApplicationDbContext.Orders so the handler can load the order via
    /// .Include(o => o.Items).FirstOrDefaultAsync(...).
    /// Passing null simulates "order not found" (empty DbSet ? returns null).
    /// </summary>
    private void SetupTrackedOrder(Order? order)
    {
        var data = order is null ? new List<Order>() : new List<Order> { order };
        var mockDbSet = data.AsQueryable().BuildMockDbSet();
        _contextMock.Setup(x => x.Orders).Returns(mockDbSet.Object);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsNotFoundException()
    {
        SetupTrackedOrder(null);
        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OrderBelongsToDifferentRetailer_ThrowsNotFoundException()
    {
        var order = BuildOrder(retailerId: Guid.NewGuid());
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_InvalidStatusValue_ThrowsBusinessRuleException()
    {
        var order = BuildOrder();
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, "InvalidStatus");

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ORDER_INVALID_STATUS");
    }

    [Theory]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Processing)]
    [InlineData(OrderStatus.NotProcessed, OrderStatus.Cancelled)]
    public async Task Handle_ValidTransition_FromNotProcessed_ReturnsSuccess(string from, string to)
    {
        var order = BuildOrder(from);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, to);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        order.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    public async Task Handle_ValidTransition_FromProcessing_ReturnsSuccess(string from, string to)
    {
        var order = BuildOrder(from);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, to);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(to);
    }

    [Fact]
    public async Task Handle_ValidTransition_FromShippedToDelivered_ReturnsSuccess()
    {
        var order = BuildOrder(OrderStatus.Shipped);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Delivered);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public async Task Handle_InvalidTransition_FromDeliveredTerminalState_ThrowsBusinessRuleException()
    {
        var order = BuildOrder(OrderStatus.Delivered);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Cancelled);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public async Task Handle_InvalidTransition_FromCancelledTerminalState_ThrowsBusinessRuleException()
    {
        var order = BuildOrder(OrderStatus.Cancelled);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public async Task Handle_InvalidTransition_FromShippedToCancelled_ThrowsBusinessRuleException()
    {
        var order = BuildOrder(OrderStatus.Shipped);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Cancelled);

        var act = () => _sut.Handle(command, default);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public async Task Handle_ValidTransition_PublishesDomainEvent()
    {
        var order = BuildOrder(OrderStatus.NotProcessed);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);
        await _sut.Handle(command, default);

        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<OrderStatusChangedEvent>(e =>
                    e.OrderId == OrderId &&
                    e.RetailerId == RetailerId &&
                    e.PreviousStatus == OrderStatus.NotProcessed &&
                    e.NewStatus == OrderStatus.Processing),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidTransition_SavesChanges()
    {
        var order = BuildOrder(OrderStatus.NotProcessed);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);
        await _sut.Handle(command, default);

        _uowMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidTransition_ReturnsSuccessResult()
    {
        var order = BuildOrder(OrderStatus.NotProcessed);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);
        var result = await _sut.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ConcurrencyConflictOnAllRetries_ThrowsConflictException()
    {
        var callCount = 0;

        // Override the constructor-level ExecuteInTransactionAsync mock.
        _uowMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task> action, CancellationToken ct) =>
            {
                callCount++;
                await action(ct);
            });

        // Return a fresh NotProcessed order on every retry so the handler can re-load
        // it without seeing a mutated Status from the previous (failed) attempt.
        _contextMock
            .Setup(x => x.Orders)
            .Returns(() =>
            {
                var fresh = BuildOrder(OrderStatus.NotProcessed);
                return new List<Order> { fresh }.AsQueryable().BuildMockDbSet().Object;
            });

        _uowMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_SameStatusTransition_ThrowsBusinessRuleException()
    {
        var order = BuildOrder(OrderStatus.NotProcessed);
        SetupTrackedOrder(order);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.NotProcessed);

        var act = () => _sut.Handle(command, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_ValidTransition_DomainEventContainsCorrectOrderItems()
    {
        var order = BuildOrder(OrderStatus.NotProcessed);
        SetupTrackedOrder(order);

        OrderStatusChangedEvent? capturedEvent = null;
        _mediatorMock
            .Setup(x => x.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<INotification, CancellationToken>((e, _) =>
                capturedEvent = e as OrderStatusChangedEvent)
            .Returns(Task.CompletedTask);

        var command = new UpdateOrderStatusCommand(OrderId, RetailerId, OrderStatus.Processing);
        await _sut.Handle(command, default);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Items.Should().NotBeEmpty();
    }
}