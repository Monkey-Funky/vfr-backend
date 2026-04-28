using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.Services.Customer;

public interface IWeatherService
{
    Task<WeatherDataDto> GetCurrentWeatherAsync(string location, CancellationToken ct);
}

public sealed record WeatherDataDto(
    string Condition,
    decimal TemperatureF,
    decimal WindSpeedMph,
    string Description
);
