using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit.Domain;

public sealed class SubscriptionTests
{
    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public void IsActive_StatusActiveEndDateFutureIsDeletedFalse_ReturnsTrue()
    {
        // Arrange
        var subscription = Subscription.Create(
            RetailerId,
            PlanId,
            SubscriptionStatus.Active,
            DateTime.UtcNow,
            endDate: DateTime.UtcNow.AddDays(30));

        // Act
        var result = subscription.IsActive;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsActive_StatusActiveEndDateInPast_ReturnsFalse()
    {
        // Arrange
        var subscription = Subscription.Create(
            RetailerId,
            PlanId,
            SubscriptionStatus.Active,
            DateTime.UtcNow.AddDays(-60),
            endDate: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = subscription.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_StatusSuspended_ReturnsFalse()
    {
        // Arrange
        var subscription = Subscription.Create(
            RetailerId,
            PlanId,
            SubscriptionStatus.Cancelled,
            DateTime.UtcNow,
            endDate: DateTime.UtcNow.AddDays(30));

        // Act
        var result = subscription.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_IsDeletedTrue_ReturnsFalse()
    {
        // Arrange
        var subscription = Subscription.Create(
            RetailerId,
            PlanId,
            SubscriptionStatus.Active,
            DateTime.UtcNow,
            endDate: DateTime.UtcNow.AddDays(30));

        subscription.MarkAsDeleted();

        // Act
        var result = subscription.IsActive;

        // Assert
        result.Should().BeFalse();
    }
}
