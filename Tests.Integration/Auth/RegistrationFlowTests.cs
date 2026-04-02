// tests/Tests.Integration/Auth/RegistrationFlowTests.cs

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

// ✅ FIX: Removed "using Microsoft.VisualStudio.TestPlatform.TestHost;"
// That using imported the test-framework Program class, making
// WebApplicationFactory<Program> point at the wrong entry point.

namespace Tests.Integration.Auth;

public sealed class RegistrationFlowTests : AuthIntegrationTestBase
{
    public RegistrationFlowTests(WebApplicationFactory<Program> factory) : base(factory) { }

    private const string Email = "integration.test@example.com";
    private const string Password = "Integration1!";
    private const string FullName = "Integration Test Retailer";
    private const string BrandName = "IntegrationBrand";

    // =========================================================================
    // HAPPY PATH — full Step1 → Step2 → Login → Protected Endpoint flow
    // =========================================================================

    [Fact]
    public async Task FullFlow_Step1_Step2_Login_AccessProtectedEndpoint()
    {
        // ── STEP 1 ────────────────────────────────────────────────────────────
        var step1Response = await Client.PostAsJsonAsync("/api/auth/register/step1", new
        {
            FullName,
            Email,
            Password,
            BrandName,
        });

        step1Response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Step 1 with valid input must return 200");

        using JsonDocument step1Doc = JsonDocument.Parse(
            await step1Response.Content.ReadAsStringAsync());

        string stepToken = step1Doc.RootElement.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("Step token was null");

        stepToken.Split('.').Should().HaveCount(3,
            because: "the step token is a JWT with 3 dot-separated segments");

        // ── STEP 2 ────────────────────────────────────────────────────────────
        using var step2Content = new MultipartFormDataContent();
        step2Content.Add(new StringContent(stepToken), "TempStepToken");
        step2Content.Add(new StringContent("Fashion"), "BusinessType");
        step2Content.Add(new StringContent("false"), "Has3DModels");

        var step2Response = await Client.PostAsync("/api/auth/register/step2", step2Content);

        step2Response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Step 2 with a valid step token must return 200");

        using JsonDocument step2Doc = JsonDocument.Parse(
            await step2Response.Content.ReadAsStringAsync());

        string accountStatus = step2Doc.RootElement
            .GetProperty("data")
            .GetProperty("retailerProfile")
            .GetProperty("accountStatus")
            .GetString()
            ?? throw new InvalidOperationException("accountStatus was null");

        accountStatus.Should().Be("Active",
            because: "completing Step 2 must activate the account");

        // ── LOGIN ─────────────────────────────────────────────────────────────
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Email,
            Password,
            RememberMe = false,
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument loginDoc = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        string loginAccessToken = loginDoc.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString()
            ?? throw new InvalidOperationException("login access token was null");

        loginAccessToken.Should().NotBeNullOrEmpty();

        // ── ACCESS PROTECTED ENDPOINT ─────────────────────────────────────────
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginAccessToken);

        // /api/profile doesn't need to exist — any non-401 response proves the JWT is valid.
        // If the route is not found we get 404, which satisfies "NotBe(Unauthorized)".
        var profileResponse = await Client.GetAsync("/api/profile");
        profileResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "a valid JWT must grant access to protected endpoints");
    }

    // =========================================================================
    // FAILURE — Duplicate Email → 409 Conflict
    // =========================================================================

    [Fact]
    public async Task RegisterStep1_DuplicateEmail_Returns409Conflict()
    {
        // First registration — must succeed.
        await Client.PostAsJsonAsync("/api/auth/register/step1", new
        {
            FullName,
            Email,
            Password,
            BrandName,
        });

        // Second registration with the SAME email, different brand name.
        var duplicate = await Client.PostAsJsonAsync("/api/auth/register/step1", new
        {
            FullName,
            Email,
            Password,
            BrandName = "OtherBrand",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "registering with an already-taken email must return 409 Conflict");
    }

    // =========================================================================
    // FAILURE — Invalid Step Token → 403 Forbidden
    // =========================================================================

    [Fact]
    public async Task RegisterStep2_InvalidStepToken_Returns403Forbidden()
    {
        using var step2Content = new MultipartFormDataContent();
        step2Content.Add(new StringContent("this.is.not.a.valid.jwt"), "TempStepToken");
        step2Content.Add(new StringContent("Fashion"), "BusinessType");
        step2Content.Add(new StringContent("false"), "Has3DModels");

        var response = await Client.PostAsync("/api/auth/register/step2", step2Content);

        // UnauthorizedException maps to 403 Forbidden per ExceptionHandlingMiddleware
        // (see 10-BaseApiController.md §9 and 04-CQRSConventions.md §7.1).
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "an invalid step token must be rejected with 403 Forbidden");
    }
}