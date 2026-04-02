// tests/Tests.Integration/Auth/RefreshTokenRotationTests.cs

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

public sealed class RefreshTokenRotationTests : AuthIntegrationTestBase
{
    public RefreshTokenRotationTests(WebApplicationFactory<Program> factory) : base(factory) { }

    // ── Shared helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a full Step1 → Step2 → Login sequence and returns the resulting
    /// (AccessToken, RefreshToken) pair. Used as shared setup by both rotation tests.
    ///
    /// Each test instance gets a fresh database (InitializeAsync resets it), so
    /// re-registering "rotation@example.com" is safe across sequential test runs.
    /// </summary>
    private async Task<(string AccessToken, string RefreshToken)> RegisterAndLoginAsync()
    {
        // ── STEP 1 ────────────────────────────────────────────────────────────
        var step1 = await Client.PostAsJsonAsync("/api/auth/register/step1", new
        {
            FullName = "Rotation User",
            Email = "rotation@example.com",
            Password = "Rotation1!",
            BrandName = "RotationBrand",
        });
        step1.EnsureSuccessStatusCode();

        string stepToken = JsonDocument.Parse(await step1.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("step token null");

        // ── STEP 2 ────────────────────────────────────────────────────────────
        using var step2 = new MultipartFormDataContent();
        step2.Add(new StringContent(stepToken), "TempStepToken");
        step2.Add(new StringContent("Fashion"), "BusinessType");
        step2.Add(new StringContent("false"), "Has3DModels");

        (await Client.PostAsync("/api/auth/register/step2", step2)).EnsureSuccessStatusCode();

        // ── LOGIN ─────────────────────────────────────────────────────────────
        var login = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "rotation@example.com",
            Password = "Rotation1!",
            RememberMe = false,
        });
        login.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());

        return (
            doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()
                ?? throw new InvalidOperationException("access token null"),
            doc.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()
                ?? throw new InvalidOperationException("refresh token null")
        );
    }

    // =========================================================================
    // HAPPY PATH — rotation returns a brand-new token pair
    // =========================================================================

    [Fact]
    public async Task RefreshToken_ValidRotation_ReturnsNewDistinctTokenPair()
    {
        (string accessToken, string refreshToken) = await RegisterAndLoginAsync();

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a valid rotation request must return 200 OK");

        using JsonDocument doc = JsonDocument.Parse(
            await refreshResponse.Content.ReadAsStringAsync());

        string newAccessToken = doc.RootElement.GetProperty("data")
            .GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("new access token null");

        string newRefreshToken = doc.RootElement.GetProperty("data")
            .GetProperty("refreshToken").GetString()
            ?? throw new InvalidOperationException("new refresh token null");

        newAccessToken.Should().NotBe(accessToken,
            because: "rotation must issue a new access token");

        newRefreshToken.Should().NotBe(refreshToken,
            because: "rotation must issue a new refresh token");
    }

    // =========================================================================
    // SECURITY — old refresh token is permanently rejected after rotation
    // =========================================================================

    [Fact]
    public async Task RefreshToken_OldToken_IsRejectedAfterRotation()
    {
        (string accessToken, string oldRefreshToken) = await RegisterAndLoginAsync();

        // ── First rotation — must succeed ────────────────────────────────────
        var firstRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            AccessToken = accessToken,
            RefreshToken = oldRefreshToken,
        });
        firstRefresh.EnsureSuccessStatusCode();

        // Extract the new access token issued by the first rotation.
        string newAccessToken =
            JsonDocument.Parse(await firstRefresh.Content.ReadAsStringAsync())
                .RootElement.GetProperty("data").GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("new access token null");

        // ── Attempt to reuse the old (now rotated-away) refresh token ─────────
        var secondRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            AccessToken = newAccessToken,
            RefreshToken = oldRefreshToken,   // ← old token — must be rejected
        });

        // UnauthorizedException ("hash mismatch") → ExceptionHandlingMiddleware → 403 Forbidden
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a rotated-away refresh token must be permanently rejected with 403 Forbidden");
    }
}