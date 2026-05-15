using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Profile.Commands.UpdateProfile;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CustomerProfileControllerTests : IntegrationTestBase
{
    public CustomerProfileControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WhenAuthenticated()
    {
        var response = await CustomerClient.GetAsync("/api/customer/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Email.Should().Be("test@customer.com");
    }

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var anonymousClient = Factory.CreateAnonymousClient();

        var response = await anonymousClient.GetAsync("/api/customer/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsOk_WhenDataIsValid()
    {
        var command = new UpdateCustomerProfileCommand(
            FullName: "Updated Customer Name",
            PhoneNumber: "+201234567890",
            DateOfBirth: new DateOnly(1995, 5, 15),
            Gender: "Male");

        var response = await CustomerClient.PutAsJsonAsync("/api/customer/profile", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>();
        result!.Data!.FullName.Should().Be("Updated Customer Name");
    }

    [Fact]
    public async Task ChangePassword_ReturnsError_WhenCurrentPasswordIsWrong()
    {
        var request = new { CurrentPassword = "WrongPassword", NewPassword = "NewStrongP@ss123!" };

        var response = await CustomerClient.PostAsJsonAsync(
            "/api/customer/profile/change-password", request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsOk_WhenAuthenticated()
    {
        var response = await CustomerClient.PostAsync(
            "/api/customer/profile/delete-account", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
