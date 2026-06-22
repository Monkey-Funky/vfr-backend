using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Exceptions;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        Stream frontImageStream, string frontFileName, string frontContentType,
        Stream? sideImageStream, string? sideFileName, string? sideContentType,
        decimal heightCm,
        CancellationToken ct = default)
    {
        var aiApiUrl = _configuration["AiModels:MeasurementExtractionUrl"];

        // ── Fallback for Local Development / Missing Configuration ──────────────
        // When the AI endpoint is not yet configured, return deterministic mock
        // measurements so the rest of the flow (upsert, history) can be tested
        // end-to-end without a live AI model.
        if (string.IsNullOrWhiteSpace(aiApiUrl))
        {
            _logger.LogWarning(
                "AiModels:MeasurementExtractionUrl is not configured. " +
                "Returning mock measurements for development.");

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

        // ── Real AI API call ────────────────────────────────────────────────────
        using var requestContent = new MultipartFormDataContent();

        // 1. Height as a plain form field.
        requestContent.Add(
            new StringContent(
                heightCm.ToString(global::System.Globalization.CultureInfo.InvariantCulture)),
            "user_height_cm");

        // 2. Front image.
        var frontContent = new StreamContent(frontImageStream);
        frontContent.Headers.ContentType = MediaTypeHeaderValue.Parse(frontContentType);
        requestContent.Add(frontContent, "front_image", frontFileName);

        // 3. Side image (optional).
        if (sideImageStream is not null && sideFileName is not null && sideContentType is not null)
        {
            var sideContent = new StreamContent(sideImageStream);
            sideContent.Headers.ContentType = MediaTypeHeaderValue.Parse(sideContentType);
            requestContent.Add(sideContent, "side_image", sideFileName);
        }

        _logger.LogInformation(
            "Sending images to AI model at {Url} for measurement extraction (side image: {HasSide})…",
            aiApiUrl, sideImageStream is not null);

        // Polly resilience pipeline (60 s timeout + 2 retries) is configured in DI.
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(aiApiUrl, requestContent, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error reaching AI measurement extraction service at {Url}.", aiApiUrl);
            throw new ExternalServiceException("BodyMeasurementExtractionAI",
                "Could not reach the body measurement AI service. Please try again later.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "AI model returned {StatusCode}. Response: {Body}",
                    response.StatusCode, errorBody);

                throw new BusinessRuleException(
                    "AI_EXTRACTION_FAILED",
                    "The AI model could not process the uploaded images. " +
                    "Please ensure both photos are clear, full-body views and try again.");
            }

            var responseStream = await response.Content.ReadAsStreamAsync(ct);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aiResponse = await JsonSerializer.DeserializeAsync<AiMeasureResponse>(responseStream, options, cancellationToken: ct);

            if (aiResponse?.Data?.Measurements == null)
            {
                _logger.LogError("AI measurement service returned a response that could not be parsed or contained no measurements.");
                throw new ExternalServiceException("BodyMeasurementExtractionAI",
                    "The body measurement AI service returned an unexpected response. Please try again.");
            }

            var aiData = aiResponse.Data.Measurements;

            return new BodyMeasurements(
                HeightCm: heightCm,
                WeightKg: 65m,
                ChestCm: aiData.Chest?.CircumferenceCm,
                WaistCm: aiData.Waist?.CircumferenceCm,
                HipsCm: aiData.Hip?.CircumferenceCm,
                ShoulderWidthCm: aiData.Shoulder?.WidthCm,
                InseamCm: null,
                NeckCm: aiData.Neck?.CircumferenceCm,
                ArmLengthCm: null,
                ShoeSizeEu: null,
                BodyShape: null
            );
        }
    }
    private sealed class AiMeasureResponse
    {
        [JsonPropertyName("status")] public string? Status { get; init; }
        [JsonPropertyName("data")] public AiData? Data { get; init; }
    }

    private sealed class AiData
    {
        [JsonPropertyName("scale_factor")] public decimal? ScaleFactor { get; init; }
        [JsonPropertyName("measurements")] public AiMeasurements? Measurements { get; init; }
    }

    private sealed class AiMeasurements
    {
        [JsonPropertyName("shoulder")] public AiBodyPart? Shoulder { get; init; }
        [JsonPropertyName("neck")] public AiBodyPart? Neck { get; init; }
        [JsonPropertyName("chest")] public AiBodyPart? Chest { get; init; }
        [JsonPropertyName("waist")] public AiBodyPart? Waist { get; init; }
        [JsonPropertyName("hip")] public AiBodyPart? Hip { get; init; }
    }

    private sealed class AiBodyPart
    {
        [JsonPropertyName("width_cm")] public decimal? WidthCm { get; init; }
        [JsonPropertyName("depth_cm")] public decimal? DepthCm { get; init; }
        [JsonPropertyName("circumference_cm")] public decimal? CircumferenceCm { get; init; }
    }
}
