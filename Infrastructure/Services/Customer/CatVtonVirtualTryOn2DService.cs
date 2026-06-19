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
/// Calls the free Hugging Face Spaces Gradio REST API (no API key, no credit card).
///
/// Flow:
///   1. Validate image URLs are publicly reachable.
///   2. POST /gradio_api/call/submit_function → event_id  (retry on Space cold-start 503).
///   3. GET  /gradio_api/call/submit_function/{event_id}  → single SSE stream → result URL.
///   4. Download the temporary HF result image.
///   5. Upload to Cloudinary via IFileStorageService for a permanent URL.
///   6. Return TryOn2DResult with the permanent Cloudinary URL.
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

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Main entry point
    // ══════════════════════════════════════════════════════════════════════════════════

    public async Task<TryOn2DResult> ProcessTryOnAsync(TryOn2DRequest request, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
            throw new BusinessRuleException("TryOn2DDisabled", "2D try-on is not enabled.");

        if (string.IsNullOrWhiteSpace(request.PersonImageUrl))
            throw new BusinessRuleException("TryOn2DMissingPerson", "A person image is required for 2D try-on.");

        if (string.IsNullOrWhiteSpace(request.GarmentImageUrl))
            throw new BusinessRuleException("TryOn2DMissingGarment", "A garment image is required for 2D try-on.");

        // ── Preflight: verify images are publicly reachable ───────────────────────────
        // Skip strict validation for Cloudinary URLs (they always return 200 on HEAD).
        // Only hard-fail on completely unreachable hosts.
        await ValidateImageUrlAsync(request.PersonImageUrl, "person", cancellationToken);
        await ValidateImageUrlAsync(request.GarmentImageUrl, "garment", cancellationToken);

        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "[CatVTON] Starting 2D try-on. CustomerId={CustomerId}, ProductId={ProductId}, " +
            "Person={Person}, Garment={Garment}, ClothType={ClothType}",
            request.CustomerId, request.ProductId,
            request.PersonImageUrl, request.GarmentImageUrl, _settings.ClothType);

        // ── Step 1: Submit ────────────────────────────────────────────────────────────
        // Retry up to MaxSubmitRetries times to handle HF Space cold starts (503).
        var eventId = await SubmitWithRetryAsync(
            request.PersonImageUrl, request.GarmentImageUrl, cancellationToken);

        _logger.LogInformation("[CatVTON] Job submitted. EventId={EventId}", eventId);

        // ── Step 2: Read SSE stream (one connection, kept open until complete) ────────
        var tempResultUrl = await ReadSseResultAsync(eventId, cancellationToken);

        _logger.LogInformation("[CatVTON] Processing done. TempUrl={TempUrl}", tempResultUrl);

        // ── Step 3: Persist to Cloudinary ─────────────────────────────────────────────
        var permanentUrl = await PersistResultImageAsync(
            tempResultUrl, request.CustomerId, request.ProductId, cancellationToken);

        var durationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;

        _logger.LogInformation(
            "[CatVTON] Completed in {Duration}s. PermanentUrl={Url}",
            durationSeconds, permanentUrl);

        return new TryOn2DResult(
            ResultImageUrl: permanentUrl,
            ConfidenceScore: 0.90m,
            DurationSeconds: durationSeconds,
            Provider: _settings.Provider);
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 1: Submit with retry (handles HF Space cold-start 503s)
    // ══════════════════════════════════════════════════════════════════════════════════

    private async Task<string> SubmitWithRetryAsync(
        string personImageUrl, string garmentImageUrl, CancellationToken ct)
    {
        var baseUrl   = _settings.SpaceUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}{_settings.ApiEndpoint}";

        // Build Gradio payload.
        // person_image → ImageEditor: { background: FileData, layers:[], composite:null }
        // cloth_image  → Image:       FileData with url
        var payload = new GradioSubmitPayload
        {
            Data = new object[]
            {
                new GradioImageEditorInput
                {
                    Background = new GradioFileData
                    {
                        Url      = personImageUrl,
                        OrigName = ExtractFileName(personImageUrl, "person.jpg"),
                        Meta     = new GradioMeta()
                    },
                    Layers    = Array.Empty<object>(),
                    Composite = null
                },
                new GradioFileData
                {
                    Url      = garmentImageUrl,
                    OrigName = ExtractFileName(garmentImageUrl, "garment.jpg"),
                    Meta     = new GradioMeta()
                },
                _settings.ClothType,
                (double)_settings.NumInferenceSteps,
                _settings.GuidanceScale,
                (double)_settings.Seed,
                "result only"           // show_type: only the result, no side-by-side
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload, JsonOptions);
        _logger.LogDebug("[CatVTON] Submit payload: {Payload}", jsonPayload);

        // Retry up to MaxSubmitRetries (default 5) with increasing delays.
        // Free HF Spaces return 503 while waking up (~30-60s cold start).
        const int maxRetries   = 6;
        int[]     delaysMs     = [3000, 8000, 15000, 20000, 25000, 30000];

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var client  = _httpClientFactory.CreateClient("catvton");
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(submitUrl, content, ct);

                // 200 → extract event_id
                if (response.IsSuccessStatusCode)
                {
                    var json    = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogDebug("[CatVTON] Submit response: {Json}", json);

                    using var doc     = JsonDocument.Parse(json);
                    var       eventId = doc.RootElement.GetProperty("event_id").GetString();

                    if (!string.IsNullOrWhiteSpace(eventId))
                        return eventId;

                    throw new ExternalServiceException("CatVTON",
                        "CatVTON submit response did not contain an event_id.");
                }

                // 503 / 502 → Space is starting up. Wait and retry.
                var statusCode = (int)response.StatusCode;
                if (statusCode is 503 or 502 or 429)
                {
                    var body  = await response.Content.ReadAsStringAsync(ct);
                    var delay = attempt < delaysMs.Length ? delaysMs[attempt] : 30000;

                    _logger.LogWarning(
                        "[CatVTON] Space returned {Status} (attempt {Attempt}/{Max}). " +
                        "Waiting {Delay}ms for cold start. Body: {Body}",
                        statusCode, attempt + 1, maxRetries, delay, body);

                    await Task.Delay(delay, ct);
                    continue;
                }

                // Other error (4xx etc.) → don't retry
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("[CatVTON] Submit failed {Status}: {Body}", statusCode, errorBody);
                throw new ExternalServiceException("CatVTON",
                    $"CatVTON submit returned HTTP {statusCode}: {errorBody}");
            }
            catch (HttpRequestException ex)
            {
                var delay = attempt < delaysMs.Length ? delaysMs[attempt] : 30000;
                _logger.LogWarning(ex,
                    "[CatVTON] Network error on submit (attempt {Attempt}/{Max}). Retrying in {Delay}ms.",
                    attempt + 1, maxRetries, delay);

                if (attempt == maxRetries - 1)
                    throw new ExternalServiceException("CatVTON",
                        $"Could not reach CatVTON HF Space after {maxRetries} attempts: {ex.Message}", ex);

                await Task.Delay(delay, ct);
            }
        }

        throw new ExternalServiceException("CatVTON",
            $"CatVTON Space did not become available after {maxRetries} submit attempts.");
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 2: Read SSE result — ONE connection, held open until complete/error/timeout
    //
    //  KEY FIX: The previous version re-opened connections in a loop — wrong.
    //  Gradio keeps the SSE connection alive and streams events until done.
    //  We open ONE connection and read it line-by-line until we see "complete" or "error".
    // ══════════════════════════════════════════════════════════════════════════════════

    private async Task<string> ReadSseResultAsync(string eventId, CancellationToken ct)
    {
        var baseUrl = _settings.SpaceUrl.TrimEnd('/');
        var pollUrl = $"{baseUrl}{_settings.ApiEndpoint}/{eventId}";

        // Use a dedicated CancellationTokenSource so we can enforce our own timeout
        // independently from the caller's token.
        using var timeoutCts  = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        using var linkedCts   = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var linkedCt          = linkedCts.Token;

        _logger.LogInformation("[CatVTON] Opening SSE stream. Url={Url}", pollUrl);

        try
        {
            using var client  = _httpClientFactory.CreateClient("catvton");
            using var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            // Tell the server we accept text/event-stream
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("text/event-stream");

            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, linkedCt);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(linkedCt);
                throw new ExternalServiceException("CatVTON",
                    $"CatVTON SSE endpoint returned HTTP {(int)response.StatusCode}: {body}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(linkedCt);
            using var reader       = new StreamReader(stream);

            string? currentEvent = null;

            while (!reader.EndOfStream)
            {
                linkedCt.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(linkedCt);
                if (line is null) continue;

                _logger.LogDebug("[CatVTON] SSE: {Line}", line);

                if (line.StartsWith("event:"))
                {
                    currentEvent = line["event:".Length..].Trim();
                    continue;
                }

                if (!line.StartsWith("data:")) continue;

                var data = line["data:".Length..].Trim();

                switch (currentEvent)
                {
                    case "error":
                        _logger.LogError("[CatVTON] Job error event. Data={Data}", data);
                        throw new ExternalServiceException("CatVTON",
                            $"CatVTON processing failed: {data}");

                    case "complete":
                        _logger.LogInformation("[CatVTON] Received 'complete' event.");
                        return ParseResultImageUrl(data);

                    case "heartbeat":
                        _logger.LogDebug("[CatVTON] Heartbeat received.");
                        break;

                    default:
                        // process_starts, process_generating, log, etc. — just log and continue
                        _logger.LogDebug("[CatVTON] Event={Event} Data={Data}", currentEvent, data);
                        break;
                }
            }

            throw new ExternalServiceException("CatVTON",
                "CatVTON SSE stream ended without a 'complete' event.");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new ExternalServiceException("CatVTON",
                $"CatVTON job timed out after {_settings.TimeoutSeconds}s.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Parse SSE "complete" data → extract result image URL
    // ══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses the SSE complete event data.
    /// Gradio returns an array of outputs: [FileData, ...]
    /// FileData shape: { "path": "...", "url": "https://...", ... }
    /// </summary>
    private string ParseResultImageUrl(string sseData)
    {
        _logger.LogDebug("[CatVTON] Parsing complete data: {Data}", sseData);

        try
        {
            using var doc  = JsonDocument.Parse(sseData);
            var       root = doc.RootElement;

            // The output is an array; the first element is the result image FileData.
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                throw new ExternalServiceException("CatVTON",
                    $"Unexpected CatVTON response format (not an array): {sseData}");

            var firstOutput = root[0];

            // Try url field first (absolute URL)
            if (firstOutput.TryGetProperty("url", out var urlProp) &&
                urlProp.ValueKind == JsonValueKind.String)
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _logger.LogInformation("[CatVTON] Result URL (from url field): {Url}", url);
                    return url;
                }
            }

            // Fall back to path field → construct file URL
            if (firstOutput.TryGetProperty("path", out var pathProp) &&
                pathProp.ValueKind == JsonValueKind.String)
            {
                var path = pathProp.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var fileUrl = $"{_settings.SpaceUrl.TrimEnd('/')}/gradio_api/file={path}";
                    _logger.LogInformation("[CatVTON] Result URL (from path field): {Url}", fileUrl);
                    return fileUrl;
                }
            }

            _logger.LogError("[CatVTON] No URL in complete data: {Data}", sseData);
            throw new ExternalServiceException("CatVTON",
                "CatVTON returned a result but no usable image URL was found.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[CatVTON] JSON parse error. RawData={Data}", sseData);
            throw new ExternalServiceException("CatVTON",
                $"Failed to parse CatVTON response JSON: {ex.Message}", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Step 3: Persist — download temp HF image → upload to Cloudinary
    // ══════════════════════════════════════════════════════════════════════════════════

    private async Task<string> PersistResultImageAsync(
        string tempUrl, Guid customerId, Guid productId, CancellationToken ct)
    {
        _logger.LogInformation("[CatVTON] Downloading result image from {Url}", tempUrl);

        try
        {
            using var client   = _httpClientFactory.CreateClient("catvton");
            var imageBytes     = await client.GetByteArrayAsync(tempUrl, ct);
            using var stream   = new MemoryStream(imageBytes);

            var fileName = $"{Guid.NewGuid():N}.jpg";
            var folder   = $"tryon-2d/{customerId:N}/{productId:N}";

            var permanentUrl = await _fileStorageService.UploadAsync(stream, fileName, folder, ct);

            if (string.IsNullOrWhiteSpace(permanentUrl))
                throw new ExternalServiceException("CatVTON",
                    "Cloudinary returned an empty URL after upload.");

            _logger.LogInformation(
                "[CatVTON] Persisted to Cloudinary. PermanentUrl={Url}", permanentUrl);

            return permanentUrl;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[CatVTON] Failed to download result from {Url}", tempUrl);
            throw new ExternalServiceException("CatVTON",
                $"Could not download CatVTON result image: {ex.Message}", ex);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Preflight — image URL validation
    // ══════════════════════════════════════════════════════════════════════════════════

    private async Task ValidateImageUrlAsync(string imageUrl, string label, CancellationToken ct)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            throw new BusinessRuleException("TryOn2DInvalidUrl",
                $"The {label} image URL is not a valid absolute URL: {imageUrl}");

        try
        {
            using var client      = _httpClientFactory.CreateClient("catvton");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, imageUrl);
            using var response    = await client.SendAsync(
                headRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[CatVTON] Preflight failed for {Label} ({Status}): {Url}",
                    label, (int)response.StatusCode, imageUrl);

                throw new ExternalServiceException("CatVTON",
                    $"The {label} image is not publicly accessible " +
                    $"(HTTP {(int)response.StatusCode}): {imageUrl}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[CatVTON] Preflight network error for {Label}: {Url}", label, imageUrl);
            throw new ExternalServiceException("CatVTON",
                $"The {label} image URL could not be reached: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════════════

    private static string ExtractFileName(string url, string fallback)
    {
        try
        {
            var uri      = new Uri(url);
            var segments = uri.Segments;
            var last     = segments.LastOrDefault()?.TrimEnd('/');
            return string.IsNullOrWhiteSpace(last) ? fallback : last;
        }
        catch
        {
            return fallback;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════════
    //  Gradio API DTOs
    // ══════════════════════════════════════════════════════════════════════════════════

    private sealed class GradioSubmitPayload
    {
        [JsonPropertyName("data")]
        public object[] Data { get; set; } = Array.Empty<object>();
    }

    private sealed class GradioImageEditorInput
    {
        [JsonPropertyName("background")]
        public GradioFileData? Background { get; set; }

        [JsonPropertyName("layers")]
        public object[] Layers { get; set; } = Array.Empty<object>();

        [JsonPropertyName("composite")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Composite { get; set; }
    }

    private sealed class GradioFileData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("orig_name")]
        public string? OrigName { get; set; }

        [JsonPropertyName("meta")]
        public GradioMeta Meta { get; set; } = new();

        [JsonPropertyName("is_stream")]
        public bool IsStream { get; set; } = false;
    }

    private sealed class GradioMeta
    {
        [JsonPropertyName("_type")]
        public string Type { get; set; } = "gradio.FileData";
    }
}
