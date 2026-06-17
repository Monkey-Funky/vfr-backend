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

        var result = await _queueClient.SubmitAndPollAsync<Sam3dBodyRequest, Sam3dBodyResponse>(
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

        // Verify the product image is publicly accessible before handing it to fal.ai.
        // fal.ai will silently fail (FAILED status after 5-30s wait) if the URL is 404.
        await ValidateImageUrlAsync(imageUrl, "product", ct);

        var requestBody = new ObjectsRequest(imageUrl, prompt, 42);
        var result = await _queueClient.SubmitAndPollAsync<ObjectsRequest, ObjectsResponse>(
            _settings.ObjectsApiId, requestBody, ct);


        // SAM 3D Objects response: model_glb can be a File object OR a direct URL string.
        // Try ModelGlb (object) first, then ModelGlbUrl (string), then individual_glbs[0].
        string? glbUrl = result.ModelGlb?.Url ?? result.ModelGlbUrl;

        if (string.IsNullOrWhiteSpace(glbUrl) && result.IndividualGlbs is { Count: > 0 })
        {
            glbUrl = result.IndividualGlbs[0].Url ?? result.IndividualGlbs[0].DirectUrl;
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

    /// <summary>
    /// Verifies an image URL is publicly reachable before handing it to fal.ai.
    /// fal.ai silently fails (FAILED status after a long wait) when it cannot fetch
    /// the URL, so we surface a clear error up front instead of waiting it out.
    /// </summary>
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

    /// <summary>
    /// SAM 3D Objects response. model_glb can be a File object OR a direct URL string.
    /// individual_glbs entries can also be either format.
    /// </summary>
    private sealed class ObjectsResponse
    {
        [JsonPropertyName("model_glb")]
        public JsonElement? ModelGlbRaw { get; init; }

        [JsonPropertyName("individual_glbs")]
        public List<IndividualGlbEntry>? IndividualGlbs { get; init; }

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

    /// <summary>An entry in individual_glbs — can be an object or a string URL.</summary>
    private sealed class IndividualGlbEntry
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }

        // Fallback when the entry is a plain URL string (not an object).
        [JsonIgnore]
        public string? DirectUrl { get; init; }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  DTOs — SAM 3D Align
    // ══════════════════════════════════════════════════════════════════════

    private sealed record AlignRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("body_mesh_url")] string BodyMeshUrl,
        [property: JsonPropertyName("object_mesh_url")] string ObjectMeshUrl,
        [property: JsonPropertyName("focal_length")] double FocalLength);

    /// <summary>
    /// SAM 3D Align response. scene_glb can be a File object OR a direct URL string.
    /// </summary>
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
