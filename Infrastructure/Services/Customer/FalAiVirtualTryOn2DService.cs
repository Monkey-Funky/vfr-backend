using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Polly.Registry;

namespace Infrastructure.Services.Customer;

/// <summary>
/// fal.ai-backed implementation of <see cref="IVirtualTryOn2DService"/>.
/// Runs a single-shot 2D image try-on (model: fal-ai/fashn/tryon) over the
/// shared fal.ai queue: a person image + a garment image in, a composited 2D image out.
/// Completely independent of the 3D SAM pipeline.
///
/// Before handing URLs to fal.ai the service performs a preflight HEAD check:
/// fal.ai silently returns FAILED when it cannot reach the image URL, which wastes
/// the full polling budget. The preflight surfaces a clean 422 or 502 immediately.
/// </summary>
public sealed class FalAiVirtualTryOn2DService : IVirtualTryOn2DService
{
    private readonly IFalAiQueueClient _queueClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VirtualTryOn2DSettings _settings;
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;
    private readonly ILogger<FalAiVirtualTryOn2DService> _logger;

    public FalAiVirtualTryOn2DService(
        IFalAiQueueClient queueClient,
        IHttpClientFactory httpClientFactory,
        IOptions<VirtualTryOn2DSettings> settings,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<FalAiVirtualTryOn2DService> logger)
    {
        _queueClient = queueClient;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _pipelineProvider = pipelineProvider;
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

        // ── Preflight: verify both images are publicly reachable ─────────────────────
        // fal.ai silently fails (FAILED status after full polling wait) when it cannot
        // fetch the image. We check upfront so we surface a clear error immediately.
        await ValidateImageUrlAsync(request.PersonImageUrl, "person", cancellationToken);
        await ValidateImageUrlAsync(request.GarmentImageUrl, "garment", cancellationToken);

        var startTime = DateTime.UtcNow;

        // FASHN accepts "auto" to detect the garment placement when no category is given.
        var category = string.IsNullOrWhiteSpace(request.Category) ? "auto" : request.Category!;

        _logger.LogInformation(
            "Starting 2D try-on. Provider: {Provider}, Model: {ModelId}, " +
            "CustomerId: {CustomerId}, ProductId: {ProductId}, " +
            "Person: {Person}, Garment: {Garment}, Category: {Category}",
            _settings.Provider, _settings.ModelId,
            request.CustomerId, request.ProductId,
            request.PersonImageUrl, request.GarmentImageUrl, category);

        var requestBody = new FashnTryOnRequest(
            ModelImage: request.PersonImageUrl,
            GarmentImage: request.GarmentImageUrl,
            Category: category);

        // ── Execute via the "tryon-2d" Polly pipeline (timeout + circuit-breaker) ────
        var pipeline = _pipelineProvider.GetPipeline("tryon-2d");

        var result = await pipeline.ExecuteAsync(async ct =>
            await _queueClient.SubmitAndPollAsync<FashnTryOnRequest, FashnTryOnResponse>(
                _settings.ModelId, requestBody, ct, _settings.TimeoutSeconds),
            cancellationToken);

        var resultImageUrl = result.Images?.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(resultImageUrl))
        {
            _logger.LogError(
                "fal.ai FASHN returned no result image for CustomerId {CustomerId}, ProductId {ProductId}.",
                request.CustomerId, request.ProductId);
            throw new ExternalServiceException("VirtualTryOn2D",
                "The 2D try-on provider processed the request but did not return a result image.");
        }

        var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "2D try-on completed in {Duration}s for CustomerId {CustomerId}. ResultImageUrl: {ResultImageUrl}",
            durationSeconds, request.CustomerId, resultImageUrl);

        return new TryOn2DResult(
            ResultImageUrl: resultImageUrl,
            ConfidenceScore: 0.95m,
            DurationSeconds: durationSeconds,
            Provider: _settings.Provider);
    }

    // ── Image URL preflight ──────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a HEAD request to verify the image URL is publicly accessible.
    /// Throws <see cref="ExternalServiceException"/> if the URL returns a non-2xx status.
    /// Throws <see cref="BusinessRuleException"/> if the URL itself is malformed or unreachable.
    /// </summary>
    private async Task ValidateImageUrlAsync(string imageUrl, string label, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new BusinessRuleException("TryOn2DEmptyUrl",
                $"The {label} image URL is empty.");

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            throw new BusinessRuleException("TryOn2DInvalidUrl",
                $"The {label} image URL is not a valid absolute URL: {imageUrl}");

        try
        {
            using var client = _httpClientFactory.CreateClient("fal-ai");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, imageUrl);
            using var response = await client.SendAsync(
                headRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Preflight check failed for {Label} image ({StatusCode}): {Url}",
                    label, (int)response.StatusCode, imageUrl);

                throw new ExternalServiceException("VirtualTryOn2D",
                    $"The {label} image is not publicly accessible " +
                    $"(HTTP {(int)response.StatusCode}): {imageUrl}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Preflight network error for {Label} image: {Url}", label, imageUrl);

            throw new ExternalServiceException("VirtualTryOn2D",
                $"The {label} image URL could not be reached: {imageUrl}. {ex.Message}");
        }
    }

    // ── fal.ai FASHN try-on contract ─────────────────────────────────────────────────
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
