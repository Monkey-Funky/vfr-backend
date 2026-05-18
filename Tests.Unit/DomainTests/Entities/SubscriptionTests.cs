using Domain.Enums.Subscription;
using Domain.Entities.Subscriptions;

namespace Tests.Unit.DomainTests.Entities;

/// <summary>
/// Unit tests for <see cref="Subscription"/> and <see cref="SubscriptionPlan"/> entities.
/// Validates subscription lifecycle state machine and plan invariants.
/// </summary>
public sealed class SubscriptionTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();
    private static readonly Guid ValidPlanId = Guid.NewGuid();

    // ── Factory Methods ──────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_ReturnsSubscriptionWithActiveStatus()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddMonths(1);

        // Act
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Active, start, end);

        // Assert
        sub.Should().NotBeNull();
        sub.RetailerId.Should().Be(ValidRetailerId);
        sub.PlanId.Should().Be(ValidPlanId);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_TrialStatus_CalculatesIsActiveCorrectly()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var trialEnd = start.AddDays(14);

        // Act
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Trial, start, null, trialEnd);

        // Assert
        sub.Status.Should().Be(SubscriptionStatus.Trial);
        sub.IsInTrial.Should().BeTrue();
        sub.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ExpiredTrial_IsActiveIsFalse()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-20);
        var trialEnd = start.AddDays(14);

        // Act
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Trial, start, null, trialEnd);

        // Assert
        sub.IsInTrial.Should().BeFalse();
        sub.IsActive.Should().BeFalse();
    }

    // ── State Machine Transitions ────────────────────────────────────────────

    [Fact]
    public void Activate_FromTrial_SetsStatusToActiveAndClearsTrial()
    {
        // Arrange
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Trial, DateTime.UtcNow, null, DateTime.UtcNow.AddDays(14));
        var newEnd = DateTime.UtcNow.AddMonths(1);

        // Act
        sub.Activate(newEnd);

        // Assert
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.EndDate.Should().Be(newEnd);
        sub.TrialEndsAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_ValidState_SetsStatusToCancelledAndEndDateToNow()
    {
        // Arrange
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        // Act
        sub.Cancel();

        // Assert
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.EndDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        sub.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpgradePlan_FromPendingDowngrade_ClearsPendingPlanAndSetsActive()
    {
        // Arrange
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.SetPendingDowngrade(Guid.NewGuid(), DateTime.UtcNow.AddMonths(1));
        
        var upgradePlanId = Guid.NewGuid();

        // Act
        sub.UpgradePlan(upgradePlanId);

        // Assert
        sub.PlanId.Should().Be(upgradePlanId);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PendingDowngradePlanId.Should().BeNull();
    }

    [Fact]
    public void Activate_InvalidState_ThrowsBusinessRuleException()
    {
        // Arrange
        var sub = Subscription.Create(ValidRetailerId, ValidPlanId, SubscriptionStatus.Active, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.Cancel(); // Status → Cancelled

        // Act
        var act = () => sub.Activate(DateTime.UtcNow.AddMonths(1));

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_SUBSCRIPTION_TRANSITION");
    }

    // ── SubscriptionPlan Invariants ──────────────────────────────────────────

    [Fact]
    public void Plan_Create_NegativePrice_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => SubscriptionPlan.Create("Name", "Tier", "Cycle", -1m, "USD", 0.05m, 10, 100, "Standard");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("PLAN_PRICE_INVALID");
    }

    [Fact]
    public void Plan_Create_InvalidCommissionRate_ThrowsBusinessRuleException()
    {
        // Act
        var act = () => SubscriptionPlan.Create("Name", "Tier", "Cycle", 99m, "USD", 1.1m, 10, 100, "Standard");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("PLAN_COMMISSION_INVALID");
    }
}
