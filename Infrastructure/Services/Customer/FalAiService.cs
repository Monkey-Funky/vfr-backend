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
        _logger.LogInformation("Generating 3D body via SAM 3D Body. Image: {ImageUrl}", imageUrl);

        var requestBody = new BodyRequest
        {
            ImageUrl = imageUrl,
            ExportMeshes = true,
            Include3dKeypoints = true
        };

        var result = await SubmitAndPollAsync<BodyRequest, BodyResponse>(
            _settings.BodyApiId, requestBody, ct);

        var glbUrl = result.ModelGlb
            ?? throw new ExternalServiceException("FalAi", "SAM 3D Body did not return a model_glb URL.");

        // Extract focal length from metadata (needed for Align step).
        var focalLength = result.Metadata?.People?.FirstOrDefault()?.FocalLength ?? 1000.0;

        _logger.LogInformation(
            "SAM 3D Body completed. GLB: {GlbUrl}, FocalLength: {FocalLength}",
            glbUrl, focalLength);

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

        // 2. Poll
        var deadline = DateTime.UtcNow.AddSeconds(_settings.MaxPollSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(_settings.PollIntervalMs, ct);

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
    //  DTOs — SAM 3D Body
    // ══════════════════════════════════════════════════════════════════════

    private sealed class BodyRequest
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; init; } = "";

        [JsonPropertyName("export_meshes")]
        public bool ExportMeshes { get; init; } = true;

        [JsonPropertyName("include_3d_keypoints")]
        public bool Include3dKeypoints { get; init; } = true;
    }

    private sealed record BodyResponse(
        [property: JsonPropertyName("model_glb")] string? ModelGlb,
        [property: JsonPropertyName("metadata")] BodyMetadata? Metadata);

    private sealed record BodyMetadata(
        [property: JsonPropertyName("people")] List<PersonData>? People);

    private sealed record PersonData(
        [property: JsonPropertyName("focal_length")] double? FocalLength,
        [property: JsonPropertyName("keypoints_3d")] List<List<double>>? Keypoints3d);

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
