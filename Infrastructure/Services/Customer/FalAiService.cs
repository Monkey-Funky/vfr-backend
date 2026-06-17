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
        _logger.LogInformation("Generating 3D body mesh via SAM 3D Body. Image: {ImageUrl}", imageUrl);

        // SAM 3D Body produces a body mesh GLB with skeletal keypoints and camera metadata.
        // Cost: $0.02 per call. The output is purpose-built for use with sam-3/3d-align.
        // We disable MHR params (not needed for alignment) to get a leaner response.
        var requestBody = new Sam3dBodyRequest
        {
            ImageUrl = imageUrl,
            ExportMeshes = true,
            Include3dKeypoints = true,
            IncludeMhrParams = false
        };

        var result = await SubmitAndPollAsync<Sam3dBodyRequest, Sam3dBodyResponse>(
            _settings.BodyApiId, requestBody, ct);

        // SAM 3D Body returns model_glb as either a File object or a direct URL string.
        var glbUrl = result.ModelGlb?.Url
            ?? result.ModelGlbUrl
            ?? throw new ExternalServiceException("FalAi", "SAM 3D Body did not return a model_glb URL.");

        // Extract focal_length from metadata.people[0].focal_length.
        // This is critical for accurate alignment in the 3d-align step.
        double focalLength = 1000.0; // safe fallback
        if (result.Metadata?.People is { Count: > 0 })
        {
            var personFocal = result.Metadata.People[0].FocalLength;
            if (personFocal is > 0)
            {
                focalLength = personFocal.Value;
                _logger.LogInformation("SAM 3D Body focal_length extracted: {FocalLength}", focalLength);
            }
            else
            {
                _logger.LogWarning("SAM 3D Body metadata.people[0].focal_length is missing or zero. Using fallback 1000.0.");
            }
        }
        else
        {
            _logger.LogWarning("SAM 3D Body metadata.people is empty. Using fallback focal_length 1000.0.");
        }

        _logger.LogInformation("SAM 3D Body generation completed. GLB: {GlbUrl}, FocalLength: {FocalLength}", glbUrl, focalLength);
        return new FalBodyResult(glbUrl, focalLength);
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

        // SAM 3D Objects response format:
        // - For single objects: model_glb contains the combined/single mesh
        // - For multi-object: individual_glbs contains per-object meshes
        // We try model_glb first (always present), then fallback to individual_glbs[0].
        string? glbUrl = result.ModelGlb?.Url;

        if (string.IsNullOrWhiteSpace(glbUrl) && result.IndividualGlbs is { Count: > 0 })
        {
            glbUrl = result.IndividualGlbs[0].Url;
        }

        if (string.IsNullOrWhiteSpace(glbUrl))
            throw new ExternalServiceException("FalAi", "SAM 3D Objects did not return any GLB URL (checked model_glb and individual_glbs).");

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
        //    Wait before first check, then poll at interval to catch completion fast.
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
    //  DTOs — SAM 3D Body (Human Reconstruction)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class Sam3dBodyRequest
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; init; } = "";

        [JsonPropertyName("export_meshes")]
        public bool ExportMeshes { get; init; } = true;

        [JsonPropertyName("include_3d_keypoints")]
        public bool Include3dKeypoints { get; init; } = true;

        /// <summary>
        /// false = leaner metadata (skip MHR params we don't need for alignment).
        /// </summary>
        [JsonPropertyName("include_mhr_params")]
        public bool IncludeMhrParams { get; init; } = false;
    }

    /// <summary>
    /// SAM 3D Body response. model_glb can be either:
    /// - A File object with url/content_type/file_name/file_size properties
    /// - A direct URL string
    /// We handle both cases with ModelGlb (object) and ModelGlbUrl (string).
    /// </summary>
    private sealed class Sam3dBodyResponse
    {
        [JsonPropertyName("model_glb")]
        public JsonElement? ModelGlbRaw { get; init; }

        [JsonPropertyName("metadata")]
        public Sam3dBodyMetadata? Metadata { get; init; }

        /// <summary>Parsed File object if model_glb is an object.</summary>
        [JsonIgnore]
        public FileResponse? ModelGlb
        {
            get
            {
                if (ModelGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.Object)
                {
                    return JsonSerializer.Deserialize<FileResponse>(raw.GetRawText(), JsonOptions);
                }
                return null;
            }
        }

        /// <summary>Direct URL string if model_glb is a string.</summary>
        [JsonIgnore]
        public string? ModelGlbUrl
        {
            get
            {
                if (ModelGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.String)
                {
                    return raw.GetString();
                }
                return null;
            }
        }
    }

    private sealed record Sam3dBodyMetadata(
        [property: JsonPropertyName("people")] List<Sam3dBodyPersonMetadata>? People);

    private sealed record Sam3dBodyPersonMetadata(
        [property: JsonPropertyName("person_id")] int? PersonId,
        [property: JsonPropertyName("focal_length")] double? FocalLength,
        [property: JsonPropertyName("bbox")] List<double>? Bbox,
        [property: JsonPropertyName("pred_cam_t")] List<double>? PredCamT);

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Objects
    // ══════════════════════════════════════════════════════════════════════

    private sealed record ObjectsRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("seed")] int Seed);

    private sealed record ObjectsResponse(
        [property: JsonPropertyName("model_glb")] FileResponse? ModelGlb,
        [property: JsonPropertyName("individual_glbs")] List<FileResponse>? IndividualGlbs);

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

    // ══════════════════════════════════════════════════════════════════════
    //  Shared DTOs
    // ══════════════════════════════════════════════════════════════════════

    private sealed record FileResponse(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("file_size")] long? FileSize);
}
