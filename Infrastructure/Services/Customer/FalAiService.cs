using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// Production implementation of <see cref="IFalAiService"/> using the SAM 3D API suite.
/// 
/// SAM 3D is purpose-built for human + object 3D reconstruction from single images.
/// - Body: $0.02, 5-10s — accurate human body geometry with skeletal keypoints
/// - Objects: $0.02, 5-10s — photorealistic object meshes via Gaussian splatting
/// - Align: $0.02, 5-10s — perspective-correct scene composition
///
/// Uses the async queue pattern: Submit → Poll → Fetch.
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
    //  SAM 3D Body — Human Reconstruction
    // ══════════════════════════════════════════════════════════════════════

    public async Task<FalBodyResult> GenerateBody3dAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating photorealistic 3D avatar via Hyper3D Rodin. Image: {ImageUrl}", imageUrl);

        // Hyper3D Rodin produces a fully textured PBR GLB with realistic skin,
        // visible facial features, and accurate body/clothing appearance.
        // Settings: quality=high, material=PBR, single image (fuse mode).
        // NO HighPack (3× cost), NO multi-view (extra upload), NO preprocessing.
        // This is the optimal balance: great quality, one API call, ~30-60s.
        var requestBody = new RodinRequest
        {
            InputImageUrls = [imageUrl],
            Prompt = "photorealistic full-body human avatar, detailed face with clear eyes nose and mouth, " +
                     "natural skin texture, realistic clothing with fabric detail, " +
                     "proper human proportions, neutral pose, clean studio lighting, " +
                     "high resolution PBR textures, production-ready 3D character",
            Tier = "Regular",
            Quality = "high",
            Material = "PBR",
            GeometryFileFormat = "glb",
            ConditionMode = "fuse",
            TAPose = true
        };

        var result = await SubmitAndPollAsync<RodinRequest, RodinResponse>(
            _settings.BodyApiId, requestBody, ct);

        // Rodin returns model_mesh as a File object { url, content_type, file_name, file_size }
        var glbUrl = result.ModelMesh?.Url
            ?? throw new ExternalServiceException("FalAi", "Hyper3D Rodin did not return a model_mesh URL.");

        _logger.LogInformation("Hyper3D Rodin avatar generation completed. GLB: {GlbUrl}", glbUrl);

        // FocalLength not available from Rodin — use standard value for Align step.
        return new FalBodyResult(glbUrl, FocalLength: 1000.0);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SAM 3D Objects — Clothing/Item Reconstruction
    // ══════════════════════════════════════════════════════════════════════

    public async Task<FalObjectResult> GenerateObject3dAsync(
        string imageUrl, string prompt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating 3D object via SAM 3D Objects. Image: {ImageUrl}, Prompt: {Prompt}",
            imageUrl, prompt);

        var requestBody = new ObjectsRequest(imageUrl, prompt, 42);
        var result = await SubmitAndPollAsync<ObjectsRequest, ObjectsResponse>(
            _settings.ObjectsApiId, requestBody, ct);

        if (result.IndividualGlbs is not { Count: > 0 })
            throw new ExternalServiceException("FalAi", "SAM 3D Objects did not return any GLB URLs.");

        var glbUrl = result.IndividualGlbs[0].Url
            ?? throw new ExternalServiceException("FalAi", "SAM 3D Objects returned a null GLB URL.");

        _logger.LogInformation("SAM 3D Objects completed. GLB: {GlbUrl}", glbUrl);
        return new FalObjectResult(glbUrl);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SAM 3D Align — Scene Composition
    // ══════════════════════════════════════════════════════════════════════

    public async Task<string> AlignSceneAsync(
        string imageUrl, string bodyMeshUrl, string objectMeshUrl,
        double focalLength, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Aligning scene via SAM 3D Align. FocalLength: {FocalLength}", focalLength);

        var requestBody = new AlignRequest(imageUrl, bodyMeshUrl, objectMeshUrl, focalLength);
        var result = await SubmitAndPollAsync<AlignRequest, AlignResponse>(
            _settings.AlignApiId, requestBody, ct);

        var sceneUrl = result.SceneGlb?.Url
            ?? throw new ExternalServiceException("FalAi", "SAM 3D Align did not return a scene_glb URL.");

        _logger.LogInformation("SAM 3D Align completed. Scene GLB: {SceneGlbUrl}", sceneUrl);
        return sceneUrl;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Queue Pattern: Submit → Poll → Fetch
    // ══════════════════════════════════════════════════════════════════════

    private async Task<TResult> SubmitAndPollAsync<TRequest, TResult>(
        string apiId, TRequest requestBody, CancellationToken ct)
    {
        var baseUrl = _settings.QueueBaseUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}/{apiId}";

        // 1. Submit
        using var submitClient = CreateAuthorizedClient();
        using var submitResponse = await submitClient.PostAsJsonAsync(submitUrl, requestBody, JsonOptions, ct);
        await EnsureSuccessOrLogAsync(submitResponse, "submit", apiId, ct);

        var queueResult = await submitResponse.Content.ReadFromJsonAsync<QueueSubmitResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse queue submit response for {apiId}.");

        var requestId = queueResult.RequestId
            ?? throw new ExternalServiceException("FalAi", $"No request_id in queue submit response for {apiId}.");

        var statusUrl = queueResult.StatusUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/status";
        var responseUrl = queueResult.ResponseUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/response";

        _logger.LogDebug("fal.ai queued. API: {ApiId}, RequestId: {RequestId}", apiId, requestId);

        // 2. Poll — SAM 3D typically completes in 5-10s.
        //    Wait 4s before first check, then poll every 1.5s to catch completion fast.
        var deadline = DateTime.UtcNow.AddSeconds(_settings.MaxPollSeconds);
        await Task.Delay(_settings.InitialPollDelayMs, ct);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            using var pollClient = CreateAuthorizedClient();
            using var pollResponse = await pollClient.GetAsync(statusUrl, ct);
            await EnsureSuccessOrLogAsync(pollResponse, "poll", apiId, ct);

            var status = await pollResponse.Content.ReadFromJsonAsync<QueueStatusResponse>(JsonOptions, ct);
            var statusValue = status?.Status ?? "UNKNOWN";

            if (string.Equals(statusValue, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(statusValue, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("fal.ai {RequestId} failed: {Error}", requestId, status?.Error);
                throw new ExternalServiceException("FalAi", $"fal.ai processing failed: {status?.Error ?? "Unknown"}");
            }

            await Task.Delay(_settings.PollIntervalMs, ct);
        }

        if (DateTime.UtcNow >= deadline)
            throw new ExternalServiceException("FalAi", $"Timed out after {_settings.MaxPollSeconds}s.");

        // 3. Fetch
        using var fetchClient = CreateAuthorizedClient();
        using var fetchResponse = await fetchClient.GetAsync(responseUrl, ct);
        await EnsureSuccessOrLogAsync(fetchResponse, "fetch", apiId, ct);

        var rawJson = await fetchResponse.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("fal.ai result for {ApiId}: {RawJson}", apiId, rawJson);

        return JsonSerializer.Deserialize<TResult>(rawJson, JsonOptions)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse result for {requestId}.");
    }

    private async Task EnsureSuccessOrLogAsync(
        HttpResponseMessage response, string phase, string apiId, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError("fal.ai {Phase} failed for {ApiId}. {StatusCode}: {Body}",
            phase, apiId, (int)response.StatusCode, body);

        throw new HttpRequestException(
            $"fal.ai {phase} returned {(int)response.StatusCode}: {body}",
            inner: null, statusCode: response.StatusCode);
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient("fal-ai");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Key", _settings.ApiKey);
        return client;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — Queue
    // ══════════════════════════════════════════════════════════════════════

    private sealed record QueueSubmitResponse(
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("status_url")] string? StatusUrl,
        [property: JsonPropertyName("response_url")] string? ResponseUrl);

    private sealed record QueueStatusResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("error")] string? Error);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — Hyper3D Rodin (Avatar Generation)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class RodinRequest
    {
        [JsonPropertyName("input_image_urls")]
        public List<string> InputImageUrls { get; init; } = [];

        [JsonPropertyName("prompt")]
        public string Prompt { get; init; } = "";

        /// <summary>Regular = production quality mesh.</summary>
        [JsonPropertyName("tier")]
        public string Tier { get; init; } = "Regular";

        /// <summary>high | medium | low | extra-low.</summary>
        [JsonPropertyName("quality")]
        public string Quality { get; init; } = "high";

        /// <summary>PBR = physically-based rendering (realistic skin/materials).</summary>
        [JsonPropertyName("material")]
        public string Material { get; init; } = "PBR";

        [JsonPropertyName("geometry_file_format")]
        public string GeometryFileFormat { get; init; } = "glb";

        /// <summary>fuse = single-image reconstruction.</summary>
        [JsonPropertyName("condition_mode")]
        public string ConditionMode { get; init; } = "fuse";

        /// <summary>T/A-pose for clean body shape and clothing fit.</summary>
        [JsonPropertyName("TAPose")]
        public bool TAPose { get; init; } = true;
    }

    private sealed record RodinResponse(
        [property: JsonPropertyName("model_mesh")] RodinFileResponse? ModelMesh,
        [property: JsonPropertyName("seed")] int? Seed);

    private sealed record RodinFileResponse(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("file_size")] long? FileSize);

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
