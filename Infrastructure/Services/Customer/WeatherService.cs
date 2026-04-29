using System.Text.Json;
using Application.Interfaces.Services.Customer;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Services.Customer;

internal sealed class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        HttpClient httpClient,
        IDistributedCache cache,
        IConfiguration configuration,
        ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WeatherDataDto> GetCurrentWeatherAsync(string location, CancellationToken ct)
    {
        var cacheKey = $"weather_{location.ToLowerInvariant()}";

        // 1. Check the Distributed Cache (Redis) first
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrEmpty(cachedData))
        {
            _logger.LogInformation("Weather cache hit for {Location}", location);
            return JsonSerializer.Deserialize<WeatherDataDto>(cachedData)!;
        }

        _logger.LogInformation("Weather cache miss. Fetching live data for {Location}", location);

        // 2. Fetch from External API
        var baseUrl = _configuration["WeatherApi:BaseUrl"];
        var apiKey = _configuration["WeatherApi:ApiKey"];

        // NOTE: Adjust this URL format to match your specific provider (e.g., WeatherAPI, OpenWeatherMap)
        var requestUrl = $"{baseUrl}?key={apiKey}&q={Uri.EscapeDataString(location)}";

        // The Polly pipeline (configured in DI) will automatically handle retries and circuit breaking here
        var response = await _httpClient.GetAsync(requestUrl, ct);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);

        // Parse the JSON (Example structure)
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        var current = root.GetProperty("current");
        var conditionNode = current.GetProperty("condition");

        var weatherData = new WeatherDataDto(
            Condition: conditionNode.GetProperty("text").GetString() ?? "Unknown",
            TemperatureF: current.GetProperty("temp_f").GetDecimal(),
            WindSpeedMph: current.GetProperty("wind_mph").GetDecimal(),
            Description: conditionNode.GetProperty("text").GetString() ?? "Clear"
        );

        // 3. Save to Cache for 30 minutes
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(weatherData), cacheOptions, ct);

        return weatherData;
    }
}