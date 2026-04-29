using API.Controllers.BaseControllers;
using Application.Features.Customer.OutfitSuggestions.Queries.GetCurrentWeather;
using Application.Interfaces.Services.Customer;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[AllowAnonymous] // Anyone can check the weather
[SwaggerTag("Weather — Public weather data for wardrobe planning.")]
[Route("api/weather")]
public sealed class WeatherController : CustomerBaseApiController
{
    [HttpGet]
    [SwaggerOperation(
        "Get Current Weather",
        "Gets the current weather and temperature for a specific location.")]
    [ProducesResponseType(typeof(ApiResponse<WeatherDataDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentWeather(
        [FromQuery] string location,
        CancellationToken cancellationToken)
    {
        // Default to Egypt if the frontend doesn't pass a location
        var targetLocation = string.IsNullOrWhiteSpace(location) ? "Egypt" : location;

        var result = await Sender.Send(new GetCurrentWeatherQuery(targetLocation), cancellationToken);
        return OkResponse(result);
    }
}