using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// fal.ai-backed implementation of <see cref="IVirtualTryOn2DService"/>.
/// Runs a single-shot 2D image try-on (default model: fal-ai/fashn/tryon) over the
/// shared fal.ai queue: a person image + a garment image in, a composited 2D image out.
/// Completely independent of the 3D SAM pipeline.
/// </summary>
public sealed class FalAiVirtualTryOn2DService : IVirtualTryOn2DService
{
    private readonly IFalAiQueueClient _queueClient;
    private readonly VirtualTryOn2DSettings _settings;
    private readonly ILogger<FalAiVirtualTryOn2DService> _logger;

    public FalAiVirtualTryOn2DService(
        IFalAiQueueClient queueClient,
        IOptions<VirtualTryOn2DSettings> settings,
        ILogger<FalAiVirtualTryOn2DService> logger)
    {
        _queueClient = queueClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TryOn2DResult> ProcessTryOnAsync(TryOn2DRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
            throw new BusinessRuleException("TryOn2DDisabled", "2D try-on is not enabled.");

        if (string.IsNullOrWhiteSpace(request.PersonImageUrl))
            throw new BusinessRuleException("TryOn2DMissingPerson", "A person image is required for 2D try-on.");

        if (string.IsNullOrWhiteSpace(request.GarmentImageUrl))
            throw new BusinessRuleException("TryOn2DMissingGarment", "A garment image is required for 2D try-on.");

        var startTime = DateTime.UtcNow;

        // FASHN accepts "auto" to detect the garment placement when no explicit category is given.
        var category = string.IsNullOrWhiteSpace(request.Category) ? "auto" : request.Category!;

        _logger.LogInformation(
            "Starting 2D try-on. Provider: {Provider}, Model: {ModelId}, Person: {Person}, Garment: {Garment}, Category: {Category}",
            _settings.Provider, _settings.ModelId, request.PersonImageUrl, request.GarmentImageUrl, category);

        var requestBody = new FashnTryOnRequest(
            ModelImage: request.PersonImageUrl,
            GarmentImage: request.GarmentImageUrl,
            Category: category);

        var result = await _queueClient.SubmitAndPollAsync<FashnTryOnRequest, FashnTryOnResponse>(
            _settings.ModelId, requestBody, cancellationToken, _settings.TimeoutSeconds);

        var resultImageUrl = result.Images?.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(resultImageUrl))
            throw new ExternalServiceException("VirtualTryOn2D", "2D try-on provider did not return a result image.");

        var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "2D try-on completed in {Duration}s. ResultImageUrl: {ResultImageUrl}",
            durationSeconds, resultImageUrl);

        return new TryOn2DResult(
            ResultImageUrl: resultImageUrl,
            ConfidenceScore: 0.95m,
            DurationSeconds: durationSeconds,
            Provider: _settings.Provider);
    }

    // ── fal.ai FASHN try-on contract ───────────────────────────────────────
    // Input:  { model_image, garment_image, category }
    // Output: { images: [ { url, ... } ] }

    private sealed record FashnTryOnRequest(
        [property: JsonPropertyName("model_image")] string ModelImage,
        [property: JsonPropertyName("garment_image")] string GarmentImage,
        [property: JsonPropertyName("category")] string Category);

    private sealed record FashnTryOnResponse(
        [property: JsonPropertyName("images")] List<FalImage>? Images);

    private sealed record FalImage(
        [property: JsonPropertyName("url")] string? Url);
}
