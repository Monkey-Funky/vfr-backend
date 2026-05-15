using System.Net;
using System.Net.Http.Json;
using Application.Features.Subscriptions.DTOs;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class SubscriptionPlansControllerTests : IntegrationTestBase
{
    public SubscriptionPlansControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetAllPlans_ReturnsOk_WhenCalledAnonymously()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/subscription-plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<SubscriptionPlanGroupDto>>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllPlans_ReturnsOk_WhenCalledAuthenticated()
    {
        var response = await Client.GetAsync("/api/subscription-plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPlanById_ReturnsNotFound_WhenPlanDoesNotExist()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync($"/api/subscription-plans/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
