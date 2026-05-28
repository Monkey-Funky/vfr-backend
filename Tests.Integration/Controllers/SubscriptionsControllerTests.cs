//using System.Net;
//using System.Net.Http.Json;
//using API.Controllers.Subscriptions;
//using Application.Features.Subscriptions.DTOs;
//using Domain.Entities.Subscriptions;
//using Tests.Integration.Fixtures;
//using Microsoft.EntityFrameworkCore;
//using Shared.DTOs;

//namespace Tests.Integration.Controllers;

//[Collection(IntegrationTestCollection.Name)]
//public sealed class SubscriptionsControllerTests : IntegrationTestBase
//{
//    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

//    public SubscriptionsControllerTests(CustomWebApplicationFactory factory)
//        : base(factory)
//    {
//    }

//    [Fact]
//    public async Task StartTrial_ReturnsCreated_WhenNoExistingSubscription()
//    {
//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        response.StatusCode.Should().Be(HttpStatusCode.Created);
//    }

//    [Fact]
//    public async Task StartTrial_ReturnsError_WhenTrialAlreadyExists()
//    {
//        await Client.PostAsync($"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
//    }

//    [Fact]
//    public async Task GetCurrentSubscription_ReturnsSubscription_WhenTrialActive()
//    {
//        await Client.PostAsync($"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        var response = await Client.GetAsync(
//            $"/api/retailers/{_retailerId}/subscription/current");

//        response.StatusCode.Should().Be(HttpStatusCode.OK);
//        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentSubscriptionDto>>();
//        result!.Success.Should().BeTrue();
//        result.Data.Should().NotBeNull();
//    }

//    [Fact]
//    public async Task GetCurrentSubscription_ReturnsNotFound_WhenNoSubscription()
//    {
//        var response = await Client.GetAsync(
//            $"/api/retailers/{_retailerId}/subscription/current");

//        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
//    }

//    [Fact]
//    public async Task GetCurrentSubscriptionDetails_ReturnsDetails_WhenTrialActive()
//    {
//        await Client.PostAsync($"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        var response = await Client.GetAsync(
//            $"/api/retailers/{_retailerId}/subscription/current/details");

//        response.StatusCode.Should().Be(HttpStatusCode.OK);
//    }

//    [Fact]
//    public async Task CancelSubscription_ReturnsOk_WhenSubscriptionActive()
//    {
//        await Client.PostAsync($"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/cancel", null);

//        response.StatusCode.Should().Be(HttpStatusCode.OK);
//    }

//    [Fact]
//    public async Task CancelSubscription_ReturnsError_WhenNoSubscription()
//    {
//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/cancel", null);

//        response.StatusCode.Should().BeOneOf(
//            HttpStatusCode.NotFound,
//            HttpStatusCode.UnprocessableEntity);
//    }

//    [Fact]
//    public async Task StartTrial_ReturnsForbidden_WhenRetailerIdDoesNotMatch()
//    {
//        var response = await Client.PostAsync(
//            $"/api/retailers/{Guid.NewGuid()}/subscriptions/trial", null);

//        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
//    }


//    [Fact]
//    public async Task ToggleRecurringPayment_ReturnsOk_WhenSubscriptionExists()
//    {
//        await Client.PostAsync($"/api/retailers/{_retailerId}/subscriptions/trial", null);

//        var response = await Client.PatchAsync(
//            $"/api/retailers/{_retailerId}/subscription/recurring", null);

//        response.StatusCode.Should().Be(HttpStatusCode.OK);
//    }

//    [Fact]
//    public async Task SubmitSaasEnquiry_ReturnsCreated_WhenNoExistingEnquiry()
//    {
//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/saas-enquiry", null);

//        response.StatusCode.Should().Be(HttpStatusCode.Created);
//    }

//    [Fact]
//    public async Task SubmitSaasEnquiry_ReturnsConflict_WhenEnquiryAlreadyExists()
//    {
//        await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/saas-enquiry", null);

//        var response = await Client.PostAsync(
//            $"/api/retailers/{_retailerId}/subscriptions/saas-enquiry", null);

//        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
//    }
//}

