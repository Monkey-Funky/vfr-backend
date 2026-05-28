using Domain.Entities.Subscriptions;

namespace Tests.Unit.DomainTests.Entities;

public sealed class SubscriptionPlanTests
{
    private static SubscriptionPlan CreateBasicPlan(
        string name = "Basic Monthly",
        string tier = "Basic",
        string billingCycle = "Monthly",
        decimal price = 29.99m,
        decimal commissionRate = 0.05m,
        int? maxActiveProducts = 50,
        int? maxMonthlyTryOns = 100)
        => SubscriptionPlan.Create(
            name, tier, billingCycle, price, "USD",
            commissionRate, maxActiveProducts, maxMonthlyTryOns, "Email");

    [Fact]
    public void Create_ValidParameters_SetsAllProperties()
    {
        const string name = "Standard Yearly";
        const string tier = "Standard";
        const string billingCycle = "Yearly";
        const decimal price = 299.99m;
        const decimal commissionRate = 0.03m;
        const int maxProducts = 200;
        const int maxTryOns = 500;
        const string supportLevel = "Priority Email";

        var plan = SubscriptionPlan.Create(
            name, tier, billingCycle, price, "usd",
            commissionRate, maxProducts, maxTryOns, supportLevel,
            isWhiteLabel: false, includesSourceCode: false,
            includesMobileApps: false, hasSla: true, hasDedicatedTeam: false);

        plan.Id.Should().NotBeEmpty();
        plan.Name.Should().Be(name);
        plan.Tier.Should().Be(tier);
        plan.BillingCycle.Should().Be(billingCycle);
        plan.PriceAmount.Should().Be(price);
        plan.Currency.Should().Be("USD");
        plan.CommissionRate.Should().Be(commissionRate);
        plan.MaxActiveProducts.Should().Be(maxProducts);
        plan.MaxMonthlyTryOns.Should().Be(maxTryOns);
        plan.SupportLevel.Should().Be(supportLevel);
        plan.IsActive.Should().BeTrue();
        plan.HasSla.Should().BeTrue();
        plan.IsWhiteLabel.Should().BeFalse();
        plan.IncludesSourceCode.Should().BeFalse();
        plan.IncludesMobileApps.Should().BeFalse();
        plan.HasDedicatedTeam.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReturnsFalseWhenInactive()
    {
        var plan = CreateBasicPlan();
        plan.IsActive.Should().BeTrue();

        plan.Deactivate();

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void PlanLimits_AreCorrectlyEnforced()
    {
        var limitedPlan = CreateBasicPlan(maxActiveProducts: 50, maxMonthlyTryOns: 100);
        var unlimitedPlan = SubscriptionPlan.Create(
            "Enterprise Monthly", "Enterprise", "Monthly",
            499.99m, "USD", 0.01m,
            null, null, "Dedicated");

        limitedPlan.MaxActiveProducts.Should().Be(50);
        limitedPlan.MaxMonthlyTryOns.Should().Be(100);
        limitedPlan.IsUnlimited.Should().BeFalse();

        unlimitedPlan.MaxActiveProducts.Should().BeNull();
        unlimitedPlan.MaxMonthlyTryOns.Should().BeNull();
        unlimitedPlan.IsUnlimited.Should().BeTrue();
    }
}