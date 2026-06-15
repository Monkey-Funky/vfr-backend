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
/// fal.ai SAM 3D API suite using the async queue pattern (submit → poll → fetch).
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

    public async Task<FalBodyResult> GenerateBody3dAsync(string imageUrl, CancellationToken ct = default)
    {
        var requestBody = new BodyRequest(imageUrl, true, true);


        var result = await SubmitAndPollAsync<BodyRequest, BodyResponse>(
            _settings.BodyApiId, requestBody, ct);

        var glbUrl = result.ModelGlb?.Url
            ?? throw new ExternalServiceException("FalAi", "Body 3D response did not contain a model_glb URL.");

        double focalLength = 1000.0; // safe default
        if (result.Metadata?.People is { Count: > 0 })
        {
            focalLength = result.Metadata.People[0].FocalLength;
        }

        _logger.LogInformation("fal.ai Body 3D generation completed. GLB URL: {GlbUrl}", glbUrl);
        return new FalBodyResult(glbUrl, focalLength);
    }

    public async Task<FalObjectResult> GenerateObject3dAsync(string imageUrl, string prompt, CancellationToken ct = default)
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

    // ── Queue Pattern: Submit → Poll → Fetch ─────────────────────────────────

    private async Task<TResult> SubmitAndPollAsync<TRequest, TResult>(
        string apiId, TRequest requestBody, CancellationToken ct)
    {
        var baseUrl = _settings.QueueBaseUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}/{apiId}";

        // 1. Submit the request to the queue
        using var submitClient = CreateAuthorizedClient();
        _logger.LogInformation("Submitting fal.ai request to {ApiId}", apiId);

        using var submitResponse = await submitClient.PostAsJsonAsync(submitUrl, requestBody, JsonOptions, ct);
        await EnsureSuccessOrLogAsync(submitResponse, "submit", apiId, ct);

        var queueResult = await submitResponse.Content.ReadFromJsonAsync<QueueSubmitResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse queue submit response for {apiId}.");

        var requestId = queueResult.RequestId
            ?? throw new ExternalServiceException("FalAi", $"Queue submit response did not contain a request_id for {apiId}.");

        // Use server-provided URLs when available; fall back to manual construction.
        var statusUrl = queueResult.StatusUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/status";
        var responseUrl = queueResult.ResponseUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/response";

        _logger.LogInformation(
            "fal.ai request queued. API: {ApiId}, RequestId: {RequestId}, StatusUrl: {StatusUrl}, ResponseUrl: {ResponseUrl}",
            apiId, requestId, statusUrl, responseUrl);

        // 2. Poll for completion
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
            _logger.LogError("fal.ai request {RequestId} timed out after {MaxPollSeconds} seconds",
                requestId, _settings.MaxPollSeconds);
            throw new ExternalServiceException("FalAi",
                $"Processing timed out after {_settings.MaxPollSeconds} seconds.");
        }

        // 3. Fetch the result using the response URL (note: must end with /response)
        using var fetchClient = CreateAuthorizedClient();
        using var fetchResponse = await fetchClient.GetAsync(responseUrl, ct);
        await EnsureSuccessOrLogAsync(fetchResponse, "fetch-result", apiId, ct);

        var result = await fetchResponse.Content.ReadFromJsonAsync<TResult>(JsonOptions, ct)
            ?? throw new ExternalServiceException("FalAi", $"Failed to parse result for request {requestId}.");

        _logger.LogInformation("fal.ai result fetched successfully. API: {ApiId}, RequestId: {RequestId}",
            apiId, requestId);

        return result;
    }

    /// <summary>
    /// Reads the response body and logs it when the status code indicates failure,
    /// then throws <see cref="HttpRequestException"/> so callers get a clear error.
    /// </summary>
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

    // ── Queue Pattern DTOs (private nested records) ──────────────────────────

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

    // ── Shared File DTO (fal.ai returns file references as { url, content_type, … }) ─
    private sealed record FalFileResponse(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("content_type")] string? ContentType,
        [property: JsonPropertyName("file_name")] string? FileName,
        [property: JsonPropertyName("file_size")] long? FileSize);

    // ── Body API DTOs ────────────────────────────────────────────────────────

    private sealed record BodyRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("export_meshes")] bool ExportMeshes,
        [property: JsonPropertyName("include_3d_keypoints")] bool Include3dKeypoints);

    private sealed record BodyResponse(
        [property: JsonPropertyName("model_glb")]
        [property: JsonConverter(typeof(FalFileOrStringConverter))]
        FalFileResponse? ModelGlb,
        [property: JsonPropertyName("metadata")] BodyMetadata? Metadata);

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

            // It's an object — deserialize normally.
            return JsonSerializer.Deserialize<FalFileResponse>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, FalFileResponse? value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, options);
    }

    private sealed record BodyMetadata(
        [property: JsonPropertyName("people")] List<BodyPerson>? People);

    private sealed record BodyPerson(
        [property: JsonPropertyName("focal_length")] double FocalLength);

    // ── Objects API DTOs ─────────────────────────────────────────────────────

    private sealed record ObjectsRequest(
        [property: JsonPropertyName("image_url")] string ImageUrl,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("seed")] int Seed);

    private sealed record ObjectsResponse(
        [property: JsonPropertyName("individual_glbs")] List<GlbEntry>? IndividualGlbs);

    private sealed record GlbEntry(
        [property: JsonPropertyName("url")] string? Url);

    // ── Align API DTOs ───────────────────────────────────────────────────────

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
