
using Tests.Integration.Fixtures;

namespace Tests.Integration.Controllers;
[Collection(IntegrationTestCollection.Name)]
public sealed class WeatherControllerTests : IntegrationTestBase
{
    public WeatherControllerTests(CustomWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetCurrentWeather_RouteIsAccessible_WhenLocationProvided()
    {
        var anonClient = Factory.CreateAnonymousClient();

        var response = await anonClient.GetAsync("/api/weather?location=Cairo");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetCurrentWeather_RouteIsAccessible_WhenLocationIsEmpty()
    {
        var anonClient = Factory.CreateAnonymousClient();

        var response = await anonClient.GetAsync("/api/weather?location=");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetCurrentWeather_IsAnonymous_WhenCalledWithoutAuth()
    {
        var anonClient = Factory.CreateAnonymousClient();

        var response = await anonClient.GetAsync("/api/weather?location=Alexandria");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCurrentWeather_IsAccessible_WhenCalledWithAuth()
    {
        var response = await CustomerClient.GetAsync("/api/weather?location=Giza");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}