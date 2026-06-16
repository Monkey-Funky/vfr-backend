using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// Production implementation of <see cref="IFalAiService"/> that integrates with the
/// fal.ai API suite using the async queue pattern (submit → poll → fetch).
///
/// Pipeline for avatar generation:
///   Raw Photo → BiRefNet (background removal) → AuraSR (upscale) → Rodin (3D)
///
/// Clothing and alignment still use SAM 3D Objects / Align APIs.
/// </summary>
public sealed class FalAiService : IFalAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FalAiSettings _settings;
    private readonly ILogger<FalAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FalAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<FalAiSettings> settings,
        ILogger<FalAiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  IMAGE PREPROCESSING
    // ══════════════════════════════════════════════════════════════════════

    public async Task<string> RemoveBackgroundAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing background via BiRefNet v2 (Portrait). Image: {ImageUrl}", imageUrl);

        var requestBody = new BiRefNetRequest
        {
            ImageUrl = imageUrl,
            Model = "Portrait",
            OperatingResolution = "2048x2048",
            OutputFormat = "png",
            RefineForeground = true
        };

        var result = await SubmitAndPollAsync<BiRefNetRequest, BiRefNetResponse>(
            _settings.BackgroundRemovalApiId, requestBody, ct);

        var cleanUrl = result.Image?.Url
            ?? throw new ExternalServiceException("FalAi", "BiRefNet background removal did not return an image URL.");

        _logger.LogInformation("Background removed successfully. Clean image: {CleanUrl}", cleanUrl);
        return cleanUrl;
    }

    public async Task<string> UpscaleImageAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Upscaling image via AuraSR. Image: {ImageUrl}", imageUrl);

        var requestBody = new AuraSrRequest
        {
            ImageUrl = imageUrl,
            UpscaleFactor = 4,
            OverlappingTiles = true,
            Checkpoint = "v2"
        };

        var result = await SubmitAndPollAsync<AuraSrRequest, AuraSrResponse>(
            _settings.ImageUpscaleApiId, requestBody, ct);

        var upscaledUrl = result.Image?.Url
            ?? throw new ExternalServiceException("FalAi", "AuraSR upscale did not return an image URL.");

        _logger.LogInformation("Image upscaled successfully. Upscaled image: {UpscaledUrl}", upscaledUrl);
        return upscaledUrl;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  3D AVATAR GENERATION (Hyper3D Rodin with multi-view)
    // ══════════════════════════════════════════════════════════════════════

    public async Task<FalAvatarResult> GenerateAvatar3dAsync(
        IReadOnlyList<string> imageUrls, CancellationToken ct = default)
    {
        // Use "concat" mode when multiple images are provided (front+side),
        // "fuse" mode for a single image.
        var conditionMode = imageUrls.Count > 1 ? "concat" : "fuse";

        var requestBody = new RodinRequest
        {
            InputImageUrls = imageUrls.ToList(),
            Prompt = "photorealistic full-body 3D human avatar, sharp detailed facial features, " +
                     "clear eyes nose and mouth, natural skin texture with pores and subtle color variation, " +
                     "detailed hair strands, realistic clothing with fabric wrinkles and folds, " +
                     "proper human proportions, studio lighting, 8K resolution textures, " +
                     "cinematic quality, production-ready 3D character",
            Tier = _settings.AvatarTier,
            Quality = _settings.AvatarQuality,
            Material = _settings.AvatarMaterial,
            GeometryFileFormat = "glb",
            ConditionMode = conditionMode,
            TAPose = true,
            Addons = _settings.EnableHighPack ? "HighPack" : null
        };

        _logger.LogInformation(
            "Generating 3D avatar via Hyper3D Rodin. Images={ImageCount}, Mode={ConditionMode}, " +
            "Quality={Quality}, Material={Material}, Tier={Tier}, HighPack={HighPack}",
            imageUrls.Count, conditionMode,
            _settings.AvatarQuality, _settings.AvatarMaterial, _settings.AvatarTier,
            _settings.EnableHighPack);

        var result = await SubmitAndPollAsync<RodinRequest, RodinResponse>(
            _settings.BodyApiId, requestBody, ct);

        var glbUrl = result.ModelMesh?.Url
            ?? throw new ExternalServiceException("FalAi", "Rodin avatar response did not contain a model_mesh URL.");

        _logger.LogInformation("Hyper3D Rodin avatar generation completed. GLB URL: {GlbUrl}", glbUrl);
        return new FalAvatarResult(glbUrl);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CLOTHING 3D (SAM 3D Objects)
    // ══════════════════════════════════════════════════════════════════════

    public async Task<FalObjectResult> GenerateObject3dAsync(
        string imageUrl, string prompt, CancellationToken ct = default)
    {
        var requestBody = new ObjectsRequest(imageUrl, prompt, 42);
        var result = await SubmitAndPollAsync<ObjectsRequest, ObjectsResponse>(
            _settings.ObjectsApiId, requestBody, ct);

        if (result.IndividualGlbs is not { Count: > 0 })
            throw new ExternalServiceException("FalAi", "Objects 3D response did not contain any GLB URLs.");

        var glbUrl = result.IndividualGlbs[0].Url
            ?? throw new ExternalServiceException("FalAi", "Objects 3D response contained a null GLB URL.");

        _logger.LogInformation("fal.ai Objects 3D generation completed. GLB URL: {GlbUrl}", glbUrl);
        return new FalObjectResult(glbUrl);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SCENE ALIGNMENT (SAM 3D Align)
    // ══════════════════════════════════════════════════════════════════════

    public async Task<string> AlignSceneAsync(
        string imageUrl, string bodyMeshUrl, string objectMeshUrl,
        double focalLength, CancellationToken ct = default)
    {
        var requestBody = new AlignRequest(imageUrl, bodyMeshUrl, objectMeshUrl, focalLength);
        var result = await SubmitAndPollAsync<AlignRequest, AlignResponse>(
            _settings.AlignApiId, requestBody, ct);

        var sceneUrl = result.SceneGlb?.Url
            ?? throw new ExternalServiceException("FalAi", "Align 3D response did not contain a scene_glb URL.");

        _logger.LogInformation("fal.ai Align 3D completed. Scene GLB URL: {SceneGlbUrl}", sceneUrl);
        return sceneUrl;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  QUEUE PATTERN: Submit → Poll → Fetch
    // ══════════════════════════════════════════════════════════════════════

    private async Task<TResult> SubmitAndPollAsync<TRequest, TResult>(
        string apiId, TRequest requestBody, CancellationToken ct)
    {
        var baseUrl = _settings.QueueBaseUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}/{apiId}";

        // 1. Submit
        using var submitClient = CreateAuthorizedClient();
        _logger.LogInformation("Submitting fal.ai request to {ApiId}", apiId);

        using var submitResponse = await submitClient.PostAsJsonAsync(submitUrl, requestBody, JsonOptions, ct);
        await EnsureSuccessOrLogAsync(submitResponse, "submit", apiId, ct);

        var queueResult = await submitResponse.Content.ReadFromJsonAsync<QueueSubmitResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse queue submit response for {apiId}.");

        var requestId = queueResult.RequestId
            ?? throw new ExternalServiceException("FalAi", $"Queue submit response did not contain a request_id for {apiId}.");

        var statusUrl = queueResult.StatusUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/status";
        var responseUrl = queueResult.ResponseUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/response";

        _logger.LogInformation(
            "fal.ai request queued. API: {ApiId}, RequestId: {RequestId}",
            apiId, requestId);

        // 2. Poll
        var deadline = DateTime.UtcNow.AddSeconds(_settings.MaxPollSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(_settings.PollIntervalMs, ct);

            using var pollClient = CreateAuthorizedClient();
            using var pollResponse = await pollClient.GetAsync(statusUrl, ct);
            await EnsureSuccessOrLogAsync(pollResponse, "poll-status", apiId, ct);

            var status = await pollResponse.Content.ReadFromJsonAsync<QueueStatusResponse>(JsonOptions, ct);
            var statusValue = status?.Status ?? "UNKNOWN";

            _logger.LogDebug("fal.ai poll status for {RequestId}: {Status}", requestId, statusValue);

            if (string.Equals(statusValue, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(statusValue, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = status?.Error ?? "Unknown error";
                _logger.LogError("fal.ai request {RequestId} failed: {Error}", requestId, errorMessage);
                throw new ExternalServiceException("FalAi", $"fal.ai processing failed: {errorMessage}");
            }
        }

        if (DateTime.UtcNow >= deadline)
        {
            _logger.LogError("fal.ai request {RequestId} timed out after {MaxPollSeconds}s",
                requestId, _settings.MaxPollSeconds);
            throw new ExternalServiceException("FalAi",
                $"Processing timed out after {_settings.MaxPollSeconds} seconds.");
        }

        // 3. Fetch result
        using var fetchClient = CreateAuthorizedClient();
        using var fetchResponse = await fetchClient.GetAsync(responseUrl, ct);
        await EnsureSuccessOrLogAsync(fetchResponse, "fetch-result", apiId, ct);

        var rawJson = await fetchResponse.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("fal.ai raw result for {ApiId}, RequestId {RequestId}: {RawJson}",
            apiId, requestId, rawJson);

        var result = JsonSerializer.Deserialize<TResult>(rawJson, JsonOptions)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse result for request {requestId}.");

        _logger.LogInformation("fal.ai result fetched. API: {ApiId}, RequestId: {RequestId}", apiId, requestId);
        return result;
    }

    private async Task EnsureSuccessOrLogAsync(
        HttpResponseMessage response, string phase, string apiId, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(
            "fal.ai {Phase} failed for {ApiId}. Status: {StatusCode}, Body: {Body}",
            phase, apiId, (int)response.StatusCode, body);

        throw new HttpRequestException(
            $"fal.ai {phase} returned {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient("fal-ai");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Key", _settings.ApiKey);
        return client;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — Queue Pattern
    // ══════════════════════════════════════════════════════════════════════

    private sealed record QueueSubmitResponse(
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("status_url")] string? StatusUrl,
        [property: JsonPropertyName("response_url")] string? ResponseUrl,
        [property: JsonPropertyName("cancel_url")] string? CancelUrl);

    private sealed record QueueStatusResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("response_url")] string? ResponseUrl);

    // ── Shared file DTO ──────────────────────────────────────────────────
    private sealed record FalFileResponse(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("file_size")] long? FileSize);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — BiRefNet v2 (Background Removal)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class BiRefNetRequest
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; init; } = "";

        /// <summary>Portrait model is specifically trained for human subjects.</summary>
        [JsonPropertyName("model")]
        public string Model { get; init; } = "Portrait";

        /// <summary>2048×2048 for maximum accuracy on high-res photos.</summary>
        [JsonPropertyName("operating_resolution")]
        public string OperatingResolution { get; init; } = "2048x2048";

        [JsonPropertyName("output_format")]
        public string OutputFormat { get; init; } = "png";

        [JsonPropertyName("refine_foreground")]
        public bool RefineForeground { get; init; } = true;
    }

    private sealed record BiRefNetResponse(
        [property: JsonPropertyName("image")] BiRefNetImageFile? Image);

    private sealed record BiRefNetImageFile(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("width")] int? Width,
        [property: JsonPropertyName("height")] int? Height,
        [property: JsonPropertyName("content_type")] string? ContentType);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — AuraSR (Image Upscaling)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class AuraSrRequest
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; init; } = "";

        /// <summary>4× super-resolution for maximum detail.</summary>
        [JsonPropertyName("upscale_factor")]
        public int UpscaleFactor { get; init; } = 4;

        /// <summary>Overlapping tiles remove seam artifacts (doubles inference time).</summary>
        [JsonPropertyName("overlapping_tiles")]
        public bool OverlappingTiles { get; init; } = true;

        /// <summary>v2 checkpoint is newer and more accurate.</summary>
        [JsonPropertyName("checkpoint")]
        public string Checkpoint { get; init; } = "v2";
    }

    private sealed record AuraSrResponse(
        [property: JsonPropertyName("image")] AuraSrImageFile? Image);

    private sealed record AuraSrImageFile(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("width")] int? Width,
        [property: JsonPropertyName("height")] int? Height);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — Hyper3D Rodin (Avatar 3D)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class RodinRequest
    {
        [JsonPropertyName("input_image_urls")]
        public List<string> InputImageUrls { get; init; } = [];

        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = "";

        [JsonPropertyName("tier")]
        public string Tier { get; init; } = "Regular";

        [JsonPropertyName("quality")]
        public string Quality { get; init; } = "high";

        [JsonPropertyName("material")]
        public string Material { get; init; } = "PBR";

        [JsonPropertyName("geometry_file_format")]
        public string GeometryFileFormat { get; init; } = "glb";

        /// <summary>"concat" for multi-view, "fuse" for single image.</summary>
        [JsonPropertyName("condition_mode")]
        public string ConditionMode { get; init; } = "fuse";

        [JsonPropertyName("TAPose")]
        public bool TAPose { get; init; }

        /// <summary>HighPack: 4K textures + high-poly mesh (3× cost).</summary>
        [JsonPropertyName("addons")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Addons { get; init; }
    }

    private sealed record RodinResponse(
        [property: JsonPropertyName("model_mesh")]
        [property: JsonConverter(typeof(FalFileOrStringConverter))]
        FalFileResponse? ModelMesh,
        [property: JsonPropertyName("seed")] int? Seed);

    /// <summary>
    /// fal.ai may return File fields as either a plain URL string or a full
    /// object { url, content_type, file_name, file_size }. This converter
    /// handles both forms transparently.
    /// </summary>
    private sealed class FalFileOrStringConverter : JsonConverter<FalFileResponse?>
    {
        public override FalFileResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var url = reader.GetString();
                return new FalFileResponse(url, null, null, null);
            }

            return JsonSerializer.Deserialize<FalFileResponse>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, FalFileResponse? value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, options);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Objects
    // ══════════════════════════════════════════════════════════════════════

    private sealed record ObjectsRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("seed")] int Seed);

    private sealed record ObjectsResponse(
        [property: JsonPropertyName("individual_glbs")] List<GlbEntry>? IndividualGlbs);

    private sealed record GlbEntry(
        [property: JsonPropertyName("url")] string? Url);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Align
    // ══════════════════════════════════════════════════════════════════════

    private sealed record AlignRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("body_mesh_url")] string BodyMeshUrl,
        [property: JsonPropertyName("object_mesh_url")] string ObjectMeshUrl,
        [property: JsonPropertyName("focal_length")] double FocalLength);

    private sealed record AlignResponse(
        [property: JsonPropertyName("scene_glb")] SceneGlb? SceneGlb);

    private sealed record SceneGlb(
        [property: JsonPropertyName("url")] string? Url);
}
