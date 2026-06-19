using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// CatVTON-backed implementation of <see cref="IVirtualTryOn2DService"/>.
/// Calls the free Hugging Face Spaces Gradio REST API to perform 2D virtual try-on.
///
/// Flow:
///   1. Validate image URLs are publicly reachable (preflight HEAD check).
///   2. POST to /gradio_api/call/submit_function → receive event_id.
///   3. GET  /gradio_api/call/submit_function/{event_id} → SSE stream → result image URL.
///   4. Download the temporary HF result image.
///   5. Upload it to Cloudinary via <see cref="IFileStorageService"/> for a permanent URL.
///   6. Return <see cref="TryOn2DResult"/> with the Cloudinary URL.
///
/// No API key or credit card required — uses the public CatVTON Space.
/// </summary>
public sealed class CatVtonVirtualTryOn2DService : IVirtualTryOn2DService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFileStorageService _fileStorageService;
    private readonly CatVtonSettings _settings;
    private readonly ILogger<CatVtonVirtualTryOn2DService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CatVtonVirtualTryOn2DService(
        IHttpClientFactory httpClientFactory,
        IFileStorageService fileStorageService,
        IOptions<CatVtonSettings> settings,
        ILogger<CatVtonVirtualTryOn2DService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fileStorageService = fileStorageService;
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

        // ── Preflight: verify both images are publicly reachable ─────────────────────
        await ValidateImageUrlAsync(request.PersonImageUrl, "person", cancellationToken);
        await ValidateImageUrlAsync(request.GarmentImageUrl, "garment", cancellationToken);

        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting 2D try-on via CatVTON. SpaceUrl: {SpaceUrl}, " +
            "CustomerId: {CustomerId}, ProductId: {ProductId}, " +
            "Person: {Person}, Garment: {Garment}, ClothType: {ClothType}",
            _settings.SpaceUrl, request.CustomerId, request.ProductId,
            request.PersonImageUrl, request.GarmentImageUrl, _settings.ClothType);

        // ── Step 1: Submit the try-on request to CatVTON Gradio API ──────────────────
        var eventId = await SubmitTryOnRequestAsync(
            request.PersonImageUrl, request.GarmentImageUrl, cancellationToken);

        _logger.LogInformation("CatVTON job submitted. EventId: {EventId}", eventId);

        // ── Step 2: Poll SSE stream for the result ───────────────────────────────────
        var tempResultUrl = await PollForResultAsync(eventId, cancellationToken);

        _logger.LogInformation(
            "CatVTON processing completed. TempResultUrl: {TempResultUrl}", tempResultUrl);

        // ── Step 3: Download temp image and upload to Cloudinary for a permanent URL ─
        var permanentUrl = await PersistResultImageAsync(
            tempResultUrl, request.CustomerId, request.ProductId, cancellationToken);

        var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "2D try-on completed in {Duration}s for CustomerId {CustomerId}. ResultImageUrl: {ResultImageUrl}",
            durationSeconds, request.CustomerId, permanentUrl);

        return new TryOn2DResult(
            ResultImageUrl: permanentUrl,
            ConfidenceScore: 0.90m,
            DurationSeconds: durationSeconds,
            Provider: _settings.Provider);
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 1: Submit — POST to Gradio queue
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Submits the try-on request to the CatVTON Gradio API.
    /// Returns the event_id used to poll for results.
    /// </summary>
    private async Task<string> SubmitTryOnRequestAsync(
        string personImageUrl, string garmentImageUrl, CancellationToken ct)
    {
        var baseUrl = _settings.SpaceUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}{_settings.ApiEndpoint}";

        // Build the Gradio request payload.
        // person_image is an ImageEditor component: { background: FileData, layers: [], composite: null }
        // cloth_image is a simple Image component: FileData
        var payload = new GradioSubmitPayload
        {
            Data = new object[]
            {
                // person_image (ImageEditor format)
                new GradioImageEditorInput
                {
                    Background = new GradioFileData
                    {
                        Url = personImageUrl,
                        OrigName = "person.jpg",
                        Meta = new GradioMeta { Type = "gradio.FileData" }
                    },
                    Layers = Array.Empty<object>(),
                    Composite = null
                },
                // cloth_image (Image format)
                new GradioFileData
                {
                    Url = garmentImageUrl,
                    OrigName = "garment.jpg",
                    Meta = new GradioMeta { Type = "gradio.FileData" }
                },
                // cloth_type
                _settings.ClothType,
                // num_inference_steps
                _settings.NumInferenceSteps,
                // guidance_scale
                _settings.GuidanceScale,
                // seed
                _settings.Seed,
                // show_type — "result only" to get a clean image without the input/mask side-by-side
                "result only"
            }
        };

        try
        {
            using var client = _httpClientFactory.CreateClient("catvton");
            var jsonPayload = JsonSerializer.Serialize(payload, JsonOptions);

            _logger.LogDebug("CatVTON submit payload: {Payload}", jsonPayload);

            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(submitUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "CatVTON submit failed with {StatusCode}. Body: {Body}",
                    (int)response.StatusCode, errorBody);
                throw new ExternalServiceException("CatVTON",
                    $"CatVTON submit returned HTTP {(int)response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("CatVTON submit response: {Response}", responseJson);

            using var doc = JsonDocument.Parse(responseJson);
            var eventId = doc.RootElement.GetProperty("event_id").GetString();

            if (string.IsNullOrWhiteSpace(eventId))
                throw new ExternalServiceException("CatVTON",
                    "CatVTON submit response did not contain an event_id.");

            return eventId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "CatVTON submit network failure");
            throw new ExternalServiceException("CatVTON",
                $"Could not reach CatVTON HF Space: {ex.Message}", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 2: Poll — GET SSE stream for result
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Polls the Gradio SSE endpoint until the result is ready or timeout is reached.
    /// Parses the SSE stream to extract the result image URL.
    /// </summary>
    private async Task<string> PollForResultAsync(string eventId, CancellationToken ct)
    {
        var baseUrl = _settings.SpaceUrl.TrimEnd('/');
        var pollUrl = $"{baseUrl}{_settings.ApiEndpoint}/{eventId}";
        var deadline = DateTime.UtcNow.AddSeconds(_settings.TimeoutSeconds);

        // The Gradio SSE endpoint returns a stream of events.
        // We need to read the stream and parse the "complete" event.
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var client = _httpClientFactory.CreateClient("catvton");
                using var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "CatVTON poll returned {StatusCode}. Body: {Body}. Retrying...",
                        (int)response.StatusCode, errorBody);
                    await Task.Delay(_settings.PollIntervalMs, ct);
                    continue;
                }

                // Read the SSE stream line by line
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                string? currentEvent = null;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    _logger.LogDebug("CatVTON SSE line: {Line}", line);

                    if (line.StartsWith("event: "))
                    {
                        currentEvent = line.Substring("event: ".Length).Trim();
                    }
                    else if (line.StartsWith("data: "))
                    {
                        var data = line.Substring("data: ".Length).Trim();

                        if (currentEvent == "error")
                        {
                            _logger.LogError("CatVTON job failed. Error data: {Data}", data);
                            throw new ExternalServiceException("CatVTON",
                                $"CatVTON processing failed: {data}");
                        }

                        if (currentEvent == "complete")
                        {
                            return ParseResultImageUrl(data);
                        }
                    }
                }

                // If we finished reading the stream without a "complete" event,
                // it might be a heartbeat or the space is still processing.
                _logger.LogDebug("CatVTON SSE stream ended without completion. Retrying...");
                await Task.Delay(_settings.PollIntervalMs, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "CatVTON poll network error. Retrying in {Interval}ms.",
                    _settings.PollIntervalMs);
                await Task.Delay(_settings.PollIntervalMs, ct);
            }
        }

        throw new ExternalServiceException("CatVTON",
            $"CatVTON job timed out after {_settings.TimeoutSeconds}s waiting for results.");
    }

    /// <summary>
    /// Parses the SSE "complete" event data to extract the result image URL.
    /// The data is a JSON array, e.g.: [{"url": "https://...", "path": "...", ...}]
    /// </summary>
    private string ParseResultImageUrl(string sseData)
    {
        try
        {
            using var doc = JsonDocument.Parse(sseData);
            var root = doc.RootElement;

            // The response is an array of outputs. The first element is the result image.
            JsonElement firstOutput;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                firstOutput = root[0];
            }
            else
            {
                throw new ExternalServiceException("CatVTON",
                    $"CatVTON returned unexpected response format: {sseData}");
            }

            // The output can be a direct object with url/path, or nested in a data array
            string? resultUrl = null;

            if (firstOutput.ValueKind == JsonValueKind.Object)
            {
                // Try to get the URL directly
                if (firstOutput.TryGetProperty("url", out var urlProp) &&
                    urlProp.ValueKind == JsonValueKind.String)
                {
                    resultUrl = urlProp.GetString();
                }

                // If no url, try to construct from path
                if (string.IsNullOrWhiteSpace(resultUrl) &&
                    firstOutput.TryGetProperty("path", out var pathProp) &&
                    pathProp.ValueKind == JsonValueKind.String)
                {
                    var path = pathProp.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        resultUrl = $"{_settings.SpaceUrl.TrimEnd('/')}/gradio_api/file={path}";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(resultUrl))
            {
                _logger.LogError("CatVTON result has no usable URL. Raw data: {Data}", sseData);
                throw new ExternalServiceException("CatVTON",
                    "CatVTON processed the request but returned no result image URL.");
            }

            return resultUrl;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse CatVTON SSE result data: {Data}", sseData);
            throw new ExternalServiceException("CatVTON",
                $"Failed to parse CatVTON response: {ex.Message}", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 3: Persist — Download from HF temp URL, upload to Cloudinary
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Downloads the result image from the temporary HF Spaces URL and uploads it
    /// to Cloudinary via the existing <see cref="IFileStorageService"/> for a permanent URL.
    /// HF Spaces temporary URLs expire after a few hours.
    /// </summary>
    private async Task<string> PersistResultImageAsync(
        string tempUrl, Guid customerId, Guid productId, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("catvton");
            using var response = await client.GetAsync(tempUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Failed to download CatVTON result image. Status: {StatusCode}, URL: {Url}",
                    (int)response.StatusCode, tempUrl);
                throw new ExternalServiceException("CatVTON",
                    $"Failed to download result image from CatVTON (HTTP {(int)response.StatusCode}).");
            }

            // Read the image into a memory stream to avoid disposal issues
            var imageBytes = await response.Content.ReadAsByteArrayAsync(ct);
            using var memoryStream = new MemoryStream(imageBytes);

            // Upload to Cloudinary via the existing file storage service.
            // UploadAsync(stream, fileName, folder) — folder is the logical prefix.
            var uniqueName = $"{Guid.NewGuid():N}.png";
            var folder = $"tryon-2d/{customerId:N}/{productId:N}";

            var permanentUrl = await _fileStorageService.UploadAsync(
                memoryStream, uniqueName, folder, ct);

            if (string.IsNullOrWhiteSpace(permanentUrl))
            {
                throw new ExternalServiceException("CatVTON",
                    "Failed to upload CatVTON result to permanent storage.");
            }

            _logger.LogInformation(
                "CatVTON result persisted to Cloudinary. TempUrl: {TempUrl}, PermanentUrl: {PermanentUrl}",
                tempUrl, permanentUrl);

            return permanentUrl;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error downloading CatVTON result from {Url}", tempUrl);
            throw new ExternalServiceException("CatVTON",
                $"Could not download CatVTON result image: {ex.Message}", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Preflight — Image URL Validation
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Issues a HEAD request to verify the image URL is publicly accessible.
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
            using var client = _httpClientFactory.CreateClient("catvton");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, imageUrl);
            using var response = await client.SendAsync(
                headRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Preflight check failed for {Label} image ({StatusCode}): {Url}",
                    label, (int)response.StatusCode, imageUrl);

                throw new ExternalServiceException("CatVTON",
                    $"The {label} image is not publicly accessible " +
                    $"(HTTP {(int)response.StatusCode}): {imageUrl}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Preflight network error for {Label} image: {Url}", label, imageUrl);

            throw new ExternalServiceException("CatVTON",
                $"The {label} image URL could not be reached: {imageUrl}. {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Gradio API DTOs
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>Top-level Gradio submit request: { "data": [...] }</summary>
    private sealed class GradioSubmitPayload
    {
        [JsonPropertyName("data")]
        public object[] Data { get; set; } = Array.Empty<object>();
    }

    /// <summary>
    /// Gradio ImageEditor input format (used for person_image).
    /// The ImageEditor component expects: { background: FileData, layers: [], composite: null }
    /// </summary>
    private sealed class GradioImageEditorInput
    {
        [JsonPropertyName("background")]
        public GradioFileData? Background { get; set; }

        [JsonPropertyName("layers")]
        public object[] Layers { get; set; } = Array.Empty<object>();

        [JsonPropertyName("composite")]
        public object? Composite { get; set; }
    }

    /// <summary>
    /// Gradio FileData format. For input, either path or url must be provided.
    /// We always use url since we have publicly-accessible Cloudinary URLs.
    /// </summary>
    private sealed class GradioFileData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("orig_name")]
        public string? OrigName { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("meta")]
        public GradioMeta Meta { get; set; } = new() { Type = "gradio.FileData" };

        [JsonPropertyName("is_stream")]
        public bool IsStream { get; set; } = false;
    }

    /// <summary>Gradio meta tag required for FileData objects.</summary>
    private sealed class GradioMeta
    {
        [JsonPropertyName("_type")]
        public string Type { get; set; } = "gradio.FileData";
    }
}
