using System.Net.Http.Headers;
using System.Text.Json;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;

namespace Infrastructure.Services.Customer;

internal sealed class BodyMeasurementExtractionService : IBodyMeasurementExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BodyMeasurementExtractionService> _logger;

    public BodyMeasurementExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BodyMeasurementExtractionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BodyMeasurements> ExtractAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        decimal heightCm,
        CancellationToken ct = default)
    {
        var aiApiUrl = _configuration["AiModels:MeasurementExtractionUrl"];

        // ── Fallback for Local Development / Missing Configuration ─────────────
        if (string.IsNullOrWhiteSpace(aiApiUrl))
        {
            _logger.LogWarning("AiModels:MeasurementExtractionUrl is not configured. Returning mock measurements.");
            
            // Simulate network/processing delay (1.5 seconds)
            await Task.Delay(1500, ct);
            
            return new BodyMeasurements(
                HeightCm: heightCm, 
                WeightKg: 70m,
                ChestCm: 95m,
                WaistCm: 80m,
                HipsCm: 100m,
                ShoulderWidthCm: 45m,
                InseamCm: 82m,
                NeckCm: 38m,
                ArmLengthCm: 60m,
                ShoeSizeEu: 42m,
                BodyShape: "Rectangle"
            );
        }

        // ── Real AI API Integration ──────────────────────────────────────────
        using var requestContent = new MultipartFormDataContent();

        // 1. Add HeightCm as a regular form field
        requestContent.Add(
            new StringContent(heightCm.ToString(global::System.Globalization.CultureInfo.InvariantCulture)), 
            "heightCm");

        // 2. Add the Image file stream with correct MIME type and filename
        using var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(streamContent, "image", fileName);

        _logger.LogInformation("Sending image to AI model at {Url} for measurement extraction...", aiApiUrl);

        // Send the request (Polly handles retries/timeouts if configured in DI)
        using var response = await _httpClient.PostAsync(aiApiUrl, requestContent, ct);

        response.EnsureSuccessStatusCode();

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI API failed with status {StatusCode}. Body: {Error}", response.StatusCode, errorContent);

            throw new BusinessRuleException("AI_EXTRACTION_FAILED",
                "The AI model could not process this image. Please ensure the photo is clear and contains a full-body view.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var measurements = JsonSerializer.Deserialize<BodyMeasurements>(responseBody, options);

        if (measurements is null)
        {
            _logger.LogError("Failed to deserialize AI model response: {ResponseBody}", responseBody);
            throw new InvalidOperationException("Failed to deserialize AI model response into BodyMeasurements.");
        }

        // We ensure the returned measurements retain the actual input height 
        // in case the AI model's JSON omitted it or sent a slightly skewed estimation.
        return measurements with { HeightCm = heightCm };
    }
}
