using System.Net;
using System.Net.Http.Json;
using Application.Features.Customer.Auth.Commands.ForgotPassword;
using Application.Features.Customer.Auth.Commands.Login;
using Application.Features.Customer.Auth.Commands.RefreshToken;
using Application.Features.Customer.Auth.Commands.Register;
using Application.Features.Customer.Auth.Commands.ResetPassword;
using Application.Features.Customer.Auth.DTOs;
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CustomerAuthControllerTests : IntegrationTestBase
{
    public CustomerAuthControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenDataIsValid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new RegisterCustomerCommand(
            "New Customer",
            "newcustomer@test.com",
            "StrongP@ss123!");

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/register", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_ReturnsError_WhenCredentialsAreInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new LoginCustomerCommand(
            "test@customer.com",
            "WrongPassword123!",
            false);

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/login", command);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_RegardlessOfEmailExistence()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ForgotPasswordCustomerCommand("nonexistent@test.com");

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/forgot-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_ForExistingEmail()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ForgotPasswordCustomerCommand("test@customer.com");

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/forgot-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_ReturnsError_WhenOtpIsInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new ResetPasswordCustomerCommand(
            "test@customer.com",
            "000000",
            "NewStrongP@ss123!");

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/reset-password", command);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RefreshToken_ReturnsError_WhenTokenIsInvalid()
    {
        var anonymousClient = Factory.CreateAnonymousClient();
        var command = new RefreshCustomerTokenCommand(
            "invalid.access.token",
            "invalid-refresh-token");

        var response = await anonymousClient.PostAsJsonAsync("/api/customer/auth/refresh", command);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Logout_ReturnsOk_WhenAuthenticated()
    {
        var response = await CustomerClient.PostAsync("/api/customer/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
