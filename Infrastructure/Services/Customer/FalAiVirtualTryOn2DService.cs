using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Polly.Registry;

namespace Infrastructure.Services.Customer;

/// <summary>
/// fal.ai-backed implementation of <see cref="IVirtualTryOn2DService"/>.
/// Runs a single-shot 2D image try-on (model: fal-ai/leffa/virtual-tryon) over the
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

        // Leffa uses garment_type instead of category.
        // Map "auto" (FASHN default) to "upper_body" as a sensible Leffa default.
        var garmentType = string.IsNullOrWhiteSpace(request.Category) || request.Category == "auto"
            ? "upper_body"
            : request.Category!;

        _logger.LogInformation(
            "Starting 2D try-on. Provider: {Provider}, Model: {ModelId}, " +
            "CustomerId: {CustomerId}, ProductId: {ProductId}, " +
            "Person: {Person}, Garment: {Garment}, GarmentType: {GarmentType}",
            _settings.Provider, _settings.ModelId,
            request.CustomerId, request.ProductId,
            request.PersonImageUrl, request.GarmentImageUrl, garmentType);

        var requestBody = new LeffaTryOnRequest(
            HumanImageUrl: request.PersonImageUrl,
            GarmentImageUrl: request.GarmentImageUrl,
            GarmentType: garmentType);

        // ── Execute via the "tryon-2d" Polly pipeline (timeout + circuit-breaker) ────
        var pipeline = _pipelineProvider.GetPipeline("tryon-2d");

        var result = await pipeline.ExecuteAsync(async ct =>
            await _queueClient.SubmitAndPollAsync<LeffaTryOnRequest, LeffaTryOnResponse>(
                _settings.ModelId, requestBody, ct, _settings.TimeoutSeconds),
            cancellationToken);

        var resultImageUrl = result.Image?.Url;
        if (string.IsNullOrWhiteSpace(resultImageUrl))
        {
            _logger.LogError(
                "fal.ai Leffa returned no result image for CustomerId {CustomerId}, ProductId {ProductId}.",
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

    // ── fal.ai Leffa virtual try-on contract ─────────────────────────────────────────
    // Input:  { human_image_url, garment_image_url, garment_type }
    // Output: { image: { url, height, width, content_type } }

    private sealed record LeffaTryOnRequest(
        [property: JsonPropertyName("human_image_url")] string HumanImageUrl,
        [property: JsonPropertyName("garment_image_url")] string GarmentImageUrl,
        [property: JsonPropertyName("garment_type")] string GarmentType);

    private sealed record LeffaTryOnResponse(
        [property: JsonPropertyName("image")] LeffaImage? Image);

    private sealed record LeffaImage(
        [property: JsonPropertyName("url")] string? Url);
}
