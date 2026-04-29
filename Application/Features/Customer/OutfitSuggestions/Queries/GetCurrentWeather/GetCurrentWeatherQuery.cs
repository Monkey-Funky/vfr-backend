using Application.Interfaces.Services.Customer;

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetCurrentWeather;

public sealed record GetCurrentWeatherQuery(string Location) : IRequest<WeatherDataDto>;
