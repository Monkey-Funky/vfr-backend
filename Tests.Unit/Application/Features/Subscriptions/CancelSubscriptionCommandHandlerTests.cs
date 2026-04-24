// tests/Tests.Unit/Application/Features/Subscriptions/CancelSubscriptionCommandHandlerTests.cs
using Application.Features.Subscriptions.Commands.CancelSubscription;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums;
using Domain.Enums.Subscription;
using Domain.Exceptions;
using FluentAssertions;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Subscriptions;

public sealed class CancelSubscriptionCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<Subscription>> _subRepoMock;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly CancelSubscriptionCommandHandler _handler;

    public CancelSubscriptionCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _subRepoMock = MockRepository.Create<IRepository<Subscription>>();
        _currentUserMock = MockRepository.Create<ICurrentUserService>();
        _cacheMock = MockRepository.Create<ICacheService>();

        _unitOfWorkMock.Setup(u => u.Repository<Subscription>()).Returns(_subRepoMock.Object);

        _handler = new CancelSubscriptionCommandHandler(
            _unitOfWorkMock.Object,
            _currentUserMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_AlreadyCancelledSubscription_ThrowsBusinessRuleException()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var alreadyCancelled = Subscription.Create(
            retailerId, Guid.NewGuid(), SubscriptionStatus.Cancelled,
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1));

        _subRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyCancelled);

        var command = new CancelSubscriptionCommand();

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*SUBSCRIPTION_ALREADY_CANCELLED*");
    }

    [Fact]
    public async Task Handle_ActiveSubscription_SetsStatusToCancelledAndInvalidatesCache()
    {
        // Arrange
        Guid retailerId = Guid.NewGuid();
        _currentUserMock.SetupGet(c => c.RetailerId).Returns(retailerId);

        var activeSubscription = Subscription.Create(
            retailerId, Guid.NewGuid(), SubscriptionStatus.Active,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        _subRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Subscription, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSubscription);

        _subRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
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

        var command = new CancelSubscriptionCommand();

        // Act
        Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeSubscription.Status.Should().Be(SubscriptionStatus.Cancelled);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(
            c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}