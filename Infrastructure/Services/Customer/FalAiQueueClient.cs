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
/// duplicating queue plumbing. Behaviour is identical to the original
/// FalAiService implementation it was extracted from.
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

        // 2. Poll until COMPLETED / FAILED / timeout.
        var pollBudget = maxPollSeconds ?? _settings.MaxPollSeconds;
        var deadline = DateTime.UtcNow.AddSeconds(pollBudget);
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
            throw new ExternalServiceException("FalAi", $"Timed out after {pollBudget}s.");

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

    private sealed record QueueSubmitResponse(
        [property: JsonPropertyName("request_id")] string? RequestId,
        [property: JsonPropertyName("status_url")] string? StatusUrl,
        [property: JsonPropertyName("response_url")] string? ResponseUrl);

    private sealed record QueueStatusResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("error")] string? Error);
}
