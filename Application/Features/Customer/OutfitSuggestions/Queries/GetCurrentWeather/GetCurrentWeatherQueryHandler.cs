using Application.Interfaces.Services.Customer;

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetCurrentWeather;

internal sealed class GetCurrentWeatherQueryHandler : IRequestHandler<GetCurrentWeatherQuery, WeatherDataDto>
{
    private readonly IWeatherService _weatherService;

    public GetCurrentWeatherQueryHandler(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    public Task<WeatherDataDto> Handle(GetCurrentWeatherQuery request, CancellationToken cancellationToken)
    {
        return _weatherService.GetCurrentWeatherAsync(request.Location, cancellationToken);
    }
}
