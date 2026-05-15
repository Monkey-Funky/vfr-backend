using System.Net;
using System.Net.Http.Json;
using Application.Features.Settings.Commands.ChangePassword;
using Application.Features.Settings.Commands.UpdateNotificationPreferences;
using Application.Features.Settings.Commands.UpdateProfile;
using Application.Features.Settings.DTOs;
using Shared.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class SettingsControllerTests : IntegrationTestBase
{
    private readonly Guid _retailerId = Guid.Parse(TestAuthHandler.DefaultRetailerId);

    public SettingsControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WhenAuthenticated()
    {
        var response = await Client.GetAsync($"/api/retailers/{_retailerId}/profile");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RetailerSettingsProfileDto>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Email.Should().Be(TestAuthHandler.DefaultEmail);
    }

    [Fact]
    public async Task GetProfile_ReturnsForbidden_WhenRetailerIdDoesNotMatch()
    {
        var response = await Client.GetAsync($"/api/retailers/{Guid.NewGuid()}/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsOk_WhenDataIsValid()
    {
        var command = new UpdateProfileCommand(
            FullName: "Updated Retailer Name",
            PhoneNumber: "+201234567890",
            BrandName: null,
            BusinessType: null);

        var response = await Client.PutAsJsonAsync($"/api/retailers/{_retailerId}/profile", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_ReturnsError_WhenCurrentPasswordIsWrong()
    {
        var command = new ChangePasswordCommand(
            CurrentPassword: "WrongOldPassword",
            NewPassword: "NewStrongP@ss123!",
            ConfirmNewPassword: "NewStrongP@ss123!");

        var response = await Client.PutAsJsonAsync(
            $"/api/retailers/{_retailerId}/profile/password", command);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAvatar_ReturnsOk_WhenFileIsValid()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "avatar.jpg");

        var response = await Client.PostAsync(
            $"/api/retailers/{_retailerId}/profile/avatar", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAvatar_ReturnsOk_WhenAuthenticated()
    {
        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/profile/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadBrandLogo_ReturnsOk_WhenFileIsValid()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await Client.PostAsync(
            $"/api/retailers/{_retailerId}/profile/brand-logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteBrandLogo_ReturnsOk_WhenAuthenticated()
    {
        var response = await Client.DeleteAsync(
            $"/api/retailers/{_retailerId}/profile/brand-logo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetNotificationPreferences_ReturnsOk_WhenAuthenticated()
    {
        var response = await Client.GetAsync(
            $"/api/retailers/{_retailerId}/settings/notifications");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsOk_WhenAuthenticated()
    {
        var response = await Client.DeleteAsync($"/api/retailers/{_retailerId}/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
