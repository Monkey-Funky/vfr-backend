using System.Net;
using System.Net.Http.Json;
using Application.Features.Auth.Commands.ForgotPassword;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.RefreshToken;
using Application.Features.Auth.Commands.RegisterStep1;
using Application.Features.Auth.Commands.ResetPassword;
using Application.Features.Auth.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RegisterStep1_ReturnsCreated_WhenDataIsValid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new RegisterStep1Command(
            "New Retailer",
            "newretailer@test.com",
            "StrongP@ss123!",
            "NewBrandName");

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/register/step1", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegisterStep1_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new RegisterStep1Command(
            "Duplicate Retailer",
            TestAuthHandler.DefaultEmail,
            "StrongP@ss123!",
            "DuplicateBrand");

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/register/step1", command);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new LoginCommand(
            TestAuthHandler.DefaultEmail,
            "WrongPassword123!",
            false);

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/login", command);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_RegardlessOfEmailExistence()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ForgotPasswordCommand("nonexistent@test.com");

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/forgot-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_ForExistingEmail()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ForgotPasswordCommand(TestAuthHandler.DefaultEmail);

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/forgot-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_ReturnsUnprocessable_WhenOtpIsInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ResetPasswordCommand(
            TestAuthHandler.DefaultEmail,
            "000000",
            "NewStrongP@ss123!");

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/reset-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RefreshToken_ReturnsUnauthorized_WhenTokenIsInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new RefreshTokenCommand(
            "invalid.access.token",
            "invalid-refresh-token");

        var response = await anonymousClient.PostAsJsonAsync("/api/auth/refresh-token", command);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Logout_ReturnsNoContent_WhenAuthenticated()
    {
        var response = await Client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
