using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// Production implementation of <see cref="IFalAiService"/>.
///
/// - Avatar body: fal-ai/hyper3d/rodin (~$0.04/generation)
/// - Objects: fal-ai/sam-3/3d-objects ($0.02, 15-600s)
/// - Align: fal-ai/sam-3/3d-align ($0.02, 5-20s)
///
/// The async queue plumbing (Submit → Poll → Fetch) lives in <see cref="IFalAiQueueClient"/>.
/// </summary>
public sealed class FalAiService : IFalAiService
{
    private readonly IFalAiQueueClient _queueClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FalAiSettings _settings;
    private readonly ILogger<FalAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FalAiService(
        IFalAiQueueClient queueClient,
        IHttpClientFactory httpClientFactory,
        IOptions<FalAiSettings> settings,
        ILogger<FalAiService> logger)
    {
        _queueClient = queueClient;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Hyper3D Rodin — Avatar Generation
    // ══════════════════════════════════════════════════════════════════════

    public async Task<FalBodyResult> GenerateBody3dAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating 3D avatar via Hyper3D Rodin. Image: {ImageUrl}", imageUrl);

        var requestBody = new RodinRequest
        {
            InputImageUrls = [imageUrl],
            Quality = "high"
        };

        var result = await _queueClient.SubmitAndPollAsync<RodinRequest, RodinResponse>(
            _settings.BodyApiId, requestBody, ct);

        var glbUrl = result.ModelMesh?.Url
            ?? throw new ExternalServiceException("FalAi", "Hyper3D Rodin did not return a model_mesh URL.");

        _logger.LogInformation("Hyper3D Rodin generation completed. GLB: {GlbUrl}", glbUrl);
        return new FalBodyResult(glbUrl, 1000.0);
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

        await ValidateImageUrlAsync(imageUrl, "product", ct);

        var requestBody = new ObjectsRequest(imageUrl, prompt, 42);

        // sam-3/3d-objects is the slowest step in the pipeline — complex garments
        // (e.g. blazers) have been observed taking up to 537s on fal.ai.
        // Use ObjectsApiPollSeconds (default 700s) instead of MaxPollSeconds (300s).
        var result = await _queueClient.SubmitAndPollAsync<ObjectsRequest, ObjectsResponse>(
            _settings.ObjectsApiId, requestBody, ct, _settings.ObjectsApiPollSeconds);

        string? glbUrl = result.ModelGlb?.Url ?? result.ModelGlbUrl;

        if (string.IsNullOrWhiteSpace(glbUrl) && result.IndividualGlbs is { Count: > 0 })
        {
            var firstEntry = result.IndividualGlbs[0];
            if (firstEntry.ValueKind == JsonValueKind.String)
                glbUrl = firstEntry.GetString();
            else if (firstEntry.ValueKind == JsonValueKind.Object
                     && firstEntry.TryGetProperty("url", out var urlProp))
                glbUrl = urlProp.GetString();
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
        var result = await _queueClient.SubmitAndPollAsync<AlignRequest, AlignResponse>(
            _settings.AlignApiId, requestBody, ct);

        var sceneUrl = result.SceneGlb?.Url
            ?? result.SceneGlbUrl
            ?? throw new ExternalServiceException("FalAi", "SAM 3D Align did not return a scene_glb URL.");

        _logger.LogInformation("SAM 3D Align completed. Scene GLB: {SceneGlbUrl}", sceneUrl);
        return sceneUrl;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Preflight — Image URL Validation
    // ══════════════════════════════════════════════════════════════════════

    private async Task ValidateImageUrlAsync(string imageUrl, string label, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ExternalServiceException("FalAi", $"The {label} image URL is empty.");

        try
        {
            using var client = _httpClientFactory.CreateClient("fal-ai");
            using var request = new HttpRequestMessage(HttpMethod.Head, imageUrl);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                throw new ExternalServiceException("FalAi",
                    $"The {label} image is not publicly accessible ({(int)response.StatusCode}): {imageUrl}");
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("FalAi",
                $"The {label} image URL could not be reached: {imageUrl}. {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — Hyper3D Rodin (Avatar Generation)
    // ══════════════════════════════════════════════════════════════════════

    private sealed class RodinRequest
    {
        [JsonPropertyName("input_image_urls")]
        public List<string> InputImageUrls { get; init; } = [];

        [JsonPropertyName("condition_mode")]
        public string ConditionMode { get; init; } = "concat";

        [JsonPropertyName("geometry_file_format")]
        public string GeometryFileFormat { get; init; } = "glb";

        [JsonPropertyName("quality")]
        public string Quality { get; init; } = "high";
    }

    private sealed class RodinResponse
    {
        [JsonPropertyName("model_mesh")]
        public FileResponse? ModelMesh { get; init; }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Objects
    // ══════════════════════════════════════════════════════════════════════

    private sealed record ObjectsRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("seed")] int Seed);

    private sealed class ObjectsResponse
    {
        [JsonPropertyName("model_glb")]
        public JsonElement? ModelGlbRaw { get; init; }

        [JsonPropertyName("individual_glbs")]
        public List<JsonElement>? IndividualGlbs { get; init; }

        [JsonIgnore]
        public FileResponse? ModelGlb
        {
            get
            {
                if (ModelGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.Object)
                    return JsonSerializer.Deserialize<FileResponse>(raw.GetRawText(), JsonOptions);
                return null;
            }
        }

        [JsonIgnore]
        public string? ModelGlbUrl
        {
            get
            {
                if (ModelGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.String)
                    return raw.GetString();
                return null;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Align
    // ══════════════════════════════════════════════════════════════════════

    private sealed record AlignRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("body_mesh_url")] string BodyMeshUrl,
        [property: JsonPropertyName("object_mesh_url")] string ObjectMeshUrl,
        [property: JsonPropertyName("focal_length")] double FocalLength);

    private sealed class AlignResponse
    {
        [JsonPropertyName("scene_glb")]
        public JsonElement? SceneGlbRaw { get; init; }

        [JsonIgnore]
        public FileResponse? SceneGlb
        {
            get
            {
                if (SceneGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.Object)
                    return JsonSerializer.Deserialize<FileResponse>(raw.GetRawText(), JsonOptions);
                return null;
            }
        }

        [JsonIgnore]
        public string? SceneGlbUrl
        {
            get
            {
                if (SceneGlbRaw is not { } raw) return null;
                if (raw.ValueKind == JsonValueKind.String)
                    return raw.GetString();
                return null;
            }
        }
    }

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
