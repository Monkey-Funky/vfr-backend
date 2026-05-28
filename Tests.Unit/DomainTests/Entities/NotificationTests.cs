using Domain.Entities.Notifications;

namespace Tests.Unit.DomainTests.Entities;

public sealed class NotificationTests
{
    private static readonly Guid ValidRetailerId = Guid.NewGuid();

    [Fact]
    public void Create_ValidParameters_SetsPropertiesCorrectly()
    {
        var retailerId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var notification = Notification.Create(
            retailerId,
            Notification.NotificationType.LowStock,
            "Low Stock Alert",
            "Product SKU-001 has fallen below the threshold.",
            resourceId);

        notification.Id.Should().NotBeEmpty();
        notification.RetailerId.Should().Be(retailerId);
        notification.Type.Should().Be(Notification.NotificationType.LowStock);
        notification.Title.Should().Be("Low Stock Alert");
        notification.Body.Should().Be("Product SKU-001 has fallen below the threshold.");
        notification.ResourceId.Should().Be(resourceId);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
        notification.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_IsUnreadByDefault()
    {
        var notification = Notification.Create(
            ValidRetailerId,
            Notification.NotificationType.NewOrder,
            "New Order Received",
            "You have received a new order #12345.");

        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsRead_SetsIsReadTrue()
    {
        var notification = Notification.Create(
            ValidRetailerId,
            Notification.NotificationType.PaymentFailed,
            "Payment Failed",
            "The payment for order #99 could not be processed.");

        notification.MarkAsRead();

        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRead_SetsReadAtTimestamp()
    {
        var notification = Notification.Create(
            ValidRetailerId,
            Notification.NotificationType.SubscriptionExpiring,
            "Subscription Expiring Soon",
            "Your subscription expires in 3 days.");
        var beforeCall = DateTime.UtcNow;

        notification.MarkAsRead();

        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt!.Value.Should().BeOnOrAfter(beforeCall);
        notification.ReadAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}