using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Customer;

/// <summary>
/// Production implementation of <see cref="IFalAiQueueClient"/>.
/// Encapsulates the fal.ai async queue pattern (Submit → Poll → Fetch) so it can
/// be reused by both the 3D SAM pipeline and the 2D try-on provider without
/// duplicating queue plumbing. Authentication, base URL and poll cadence come from
/// the shared "FalAi" settings.
///
/// All HTTP failures are translated to <see cref="ExternalServiceException"/> so the
/// global exception middleware maps them to a clean 502 (Bad Gateway) instead of
/// leaking a raw HttpRequestException as a 500.
/// </summary>
public sealed class FalAiQueueClient : IFalAiQueueClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FalAiSettings _settings;
    private readonly ILogger<FalAiQueueClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FalAiQueueClient(
        IHttpClientFactory httpClientFactory,
        IOptions<FalAiSettings> settings,
        ILogger<FalAiQueueClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TResult> SubmitAndPollAsync<TRequest, TResult>(
        string apiId, TRequest requestBody, CancellationToken ct, int? maxPollSeconds = null)
    {
        var baseUrl = _settings.QueueBaseUrl.TrimEnd('/');
        var submitUrl = $"{baseUrl}/{apiId}";

        // ── 1. Submit ────────────────────────────────────────────────────────────────
        QueueSubmitResponse queueResult;
        try
        {
            using var submitClient = CreateAuthorizedClient();
            using var submitResponse = await submitClient.PostAsJsonAsync(submitUrl, requestBody, JsonOptions, ct);
            await EnsureSuccessOrThrowAsync(submitResponse, "submit", apiId, ct);

            queueResult = await submitResponse.Content.ReadFromJsonAsync<QueueSubmitResponse>(JsonOptions, ct)
                ?? throw new ExternalServiceException("FalAi", $"Failed to parse queue submit response for {apiId}.");
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure (DNS, TLS, connection refused, etc.)
            _logger.LogError(ex, "fal.ai submit network failure for {ApiId}", apiId);
            throw new ExternalServiceException("FalAi",
                $"Could not reach fal.ai to submit the job for {apiId}: {ex.Message}", ex);
        }

        var requestId = queueResult.RequestId
            ?? throw new ExternalServiceException("FalAi", $"No request_id in queue submit response for {apiId}.");

        var statusUrl = queueResult.StatusUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/status";
        var responseUrl = queueResult.ResponseUrl
            ?? $"{baseUrl}/{apiId}/requests/{requestId}/response";

        _logger.LogDebug("fal.ai job queued. API: {ApiId}, RequestId: {RequestId}", apiId, requestId);

        // ── 2. Poll until COMPLETED / FAILED / timeout ───────────────────────────────
        var pollBudget = maxPollSeconds ?? _settings.MaxPollSeconds;
        var deadline = DateTime.UtcNow.AddSeconds(pollBudget);
        await Task.Delay(_settings.InitialPollDelayMs, ct);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            QueueStatusResponse? status;
            try
            {
                using var pollClient = CreateAuthorizedClient();
                using var pollResponse = await pollClient.GetAsync(statusUrl, ct);
                await EnsureSuccessOrThrowAsync(pollResponse, "poll", apiId, ct);

                status = await pollResponse.Content.ReadFromJsonAsync<QueueStatusResponse>(JsonOptions, ct);
            }
            catch (HttpRequestException ex)
            {
                // Transient network errors during polling — log and retry the next poll cycle.
                _logger.LogWarning(ex,
                    "fal.ai poll network error for {ApiId}/{RequestId}. Retrying in {Interval}ms.",
                    apiId, requestId, _settings.PollIntervalMs);
                await Task.Delay(_settings.PollIntervalMs, ct);
                continue;
            }

            var statusValue = status?.Status ?? "UNKNOWN";
            _logger.LogDebug("fal.ai poll status for {RequestId}: {Status}", requestId, statusValue);

            if (string.Equals(statusValue, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(statusValue, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var errorMsg = status?.Error ?? "Unknown error";
                _logger.LogError("fal.ai job {RequestId} failed: {Error}", requestId, errorMsg);
                throw new ExternalServiceException("FalAi",
                    $"fal.ai processing failed for {apiId}: {errorMsg}");
            }

            await Task.Delay(_settings.PollIntervalMs, ct);
        }

        if (DateTime.UtcNow >= deadline)
            throw new ExternalServiceException("FalAi",
                $"fal.ai job {requestId} timed out after {pollBudget}s waiting for {apiId} to complete.");

        // ── 3. Fetch result ──────────────────────────────────────────────────────────
        string rawJson;
        try
        {
            using var fetchClient = CreateAuthorizedClient();
            using var fetchResponse = await fetchClient.GetAsync(responseUrl, ct);
            await EnsureSuccessOrThrowAsync(fetchResponse, "fetch", apiId, ct);

            rawJson = await fetchResponse.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "fal.ai fetch network failure for {ApiId}/{RequestId}", apiId, requestId);
            throw new ExternalServiceException("FalAi",
                $"Could not fetch result from fal.ai for {apiId}: {ex.Message}", ex);
        }

        _logger.LogDebug("fal.ai result for {ApiId} ({RequestId}): {RawJson}", apiId, requestId, rawJson);

        return JsonSerializer.Deserialize<TResult>(rawJson, JsonOptions)
            ?? throw new ExternalServiceException("FalAi",
                $"fal.ai returned an empty or unparseable response for {requestId}.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the response body on failure, logs it, then throws <see cref="ExternalServiceException"/>.
    /// Callers that also want to catch <see cref="HttpRequestException"/> (network-level) must do so
    /// themselves — this method only handles non-2xx HTTP status codes.
    /// </summary>
    private async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response, string phase, string apiId, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(
            "fal.ai {Phase} returned {StatusCode} for {ApiId}. Body: {Body}",
            phase, (int)response.StatusCode, apiId, body);

        throw new ExternalServiceException("FalAi",
            $"fal.ai {phase} for {apiId} returned HTTP {(int)response.StatusCode}: {body}");
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient("fal-ai");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Key", _settings.ApiKey);
        return client;
    }

    // ── Internal DTOs ────────────────────────────────────────────────────────────────

    private sealed record QueueSubmitResponse(
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("status_url")] string? StatusUrl,
        [property: JsonPropertyName("response_url")] string? ResponseUrl);

    private sealed record QueueStatusResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("error")] string? Error);
}
