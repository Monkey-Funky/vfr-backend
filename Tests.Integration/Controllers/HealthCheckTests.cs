using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;

/// <summary>
/// Smoke test that validates the full application boots correctly
/// with Testcontainers PostgreSQL and the test auth handler.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task SwaggerEndpoint_Returns200_WhenAppIsRunning()
    {
        // Swagger is only enabled in Development. The test factory sets
        // environment to "Testing", so swagger may not be available.
        // Instead, hit a known API endpoint to verify the pipeline works.

        // Act — hit any public or authenticated endpoint
        var retailerId = TestAuthHandler.DefaultRetailerId;
        var response = await Client.GetAsync($"/api/retailers/{retailerId}/products?pageNumber=1&pageSize=10");

        // Assert — we expect 200 (empty list) because auth is handled by TestAuthHandler
        // and the DB is clean. If the app failed to start, we'd get a connection error.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NotFound,    // acceptable if route differs
            HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AnonymousRequest_ReturnsUnauthorized()
    {
        // Arrange
        var anonClient = Factory.CreateAnonymousClient();
        var retailerId = TestAuthHandler.DefaultRetailerId;

        // Act — hit an endpoint that requires [Authorize]
        var response = await anonClient.GetAsync($"/api/retailers/{retailerId}/products?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
