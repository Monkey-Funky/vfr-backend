using System.Net;
using System.Net.Http.Json;
using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Queries.GetNotifications;
using Domain.Entities.Notifications;
using Shared.DTOs;
using Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationsControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public NotificationsControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetNotifications_ReturnsEmptyList_WhenNoNotificationsExist()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationsPagedResult>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetNotifications_ReturnsNotifications_WhenSeeded()
    {
        await SeedNotificationsAsync(3);

        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<NotificationsPagedResult>>();
        result!.Data!.Items.Should().HaveCount(3);
        result.Data.UnreadCount.Should().Be(3);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk_WhenNotificationExists()
    {
        var notificationId = await SeedSingleNotificationAsync();

        var response = await Client.PutAsync(
            $"/api/retailers/{_retailerId}/notifications/{notificationId}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.ExecuteDbContextAsync(async db =>
        {
            var notification = await db.Notifications.FirstAsync(n => n.Id == notificationId);
            notification.IsRead.Should().BeTrue();
            notification.ReadAt.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task MarkAllAsRead_ReturnsOk_WithUpdatedCount()
    {
        await SeedNotificationsAsync(5);

        var response = await Client.PutAsync(
            $"/api/retailers/{_retailerId}/notifications/read-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();
        result!.Data.Should().Be(5);
    }

    [Fact]
    public async Task DeleteNotification_ReturnsNoContent_WhenNotificationExists()
    {
        var notificationId = await SeedSingleNotificationAsync();

        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/notifications/{notificationId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteNotification_ReturnsNotFound_WhenNotificationDoesNotExist()
    {
        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/notifications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task SeedNotificationsAsync(int count)
    {
        await Factory.ExecuteDbContextAsync(async db =>
        {
            for (int i = 0; i < count; i++)
            {
                var notification = Notification.Create(
                    _retailerId,
                    Notification.NotificationType.NewOrder,
                    $"Test Notification {i + 1}",
                    $"This is test notification body {i + 1}");
                db.Notifications.Add(notification);
            }
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> SeedSingleNotificationAsync()
    {
        Guid id = Guid.Empty;
        await Factory.ExecuteDbContextAsync(async db =>
        {
            var notification = Notification.Create(
                _retailerId,
                Notification.NotificationType.LowStock,
                "Low Stock Alert",
                "Product X is running low on stock.");
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            id = notification.Id;
        });
        return id;
    }
}
