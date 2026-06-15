using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Interfaces.Services.Customer;

namespace Infrastructure.Services.Customer;

internal sealed class ComplementaryStyleService : IComplementaryStyleService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ComplementaryStyleService> _logger;

    public ComplementaryStyleService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ComplementaryStyleService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<string>> GetComplementaryItemsAsync(string productAiId, int topK, CancellationToken ct = default)
    {
        var aiApiUrl = _configuration["AiModels:StyleRecommendationUrl"];

        var requestBody = new AiStyleRequest
        {
            ProductId = productAiId,
            TopK = topK
        };

        _logger.LogInformation("Requesting {TopK} complementary items for product {ProductId} from AI...", topK, productAiId);

        using var response = await _httpClient.PostAsJsonAsync(aiApiUrl, requestBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI model returned {StatusCode}. Response: {Body}", response.StatusCode, errorBody);

            throw new BusinessRuleException("AI_RECOMMENDATION_FAILED", "Could not generate style recommendations at this time.");
        }

        var aiResponse = await response.Content.ReadFromJsonAsync<AiStyleResponse>(cancellationToken: ct);

        if (aiResponse?.Matches == null)
        {
            _logger.LogError("Failed to deserialize AI style response.");
            return new List<string>();
        }

        return aiResponse.Matches;
    }
    public async Task<List<string>> GetSimilarItemsAsync(string modelId, int topK, CancellationToken ct = default)
    {
        var aiApiUrl = _configuration["AiModels:SimilarUrl"];

        var requestBody = new AiStyleRequest
        {
            ProductId = modelId,
            TopK = topK
        };

        _logger.LogInformation("Requesting {TopK} similar items for product {ModelId} from AI...", topK, modelId);

        using var response = await _httpClient.PostAsJsonAsync(aiApiUrl, requestBody, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI similar products failed. Status: {StatusCode}. Body: {Body}", response.StatusCode, errorBody);

            throw new BusinessRuleException("AI_VALIDATION_ERROR", "The AI model rejected the product ID format.");
        }

        var aiResponse = await response.Content.ReadFromJsonAsync<AiStyleResponse>(cancellationToken: ct);

        return aiResponse?.Matches ?? new List<string>();
    }
    // ─── Private DTOs for the AI Model's JSON ────
    private sealed class AiStyleRequest
    {
        [JsonPropertyName("product_id")] public string ProductId { get; init; } = string.Empty;
        [JsonPropertyName("top_k")] public int TopK { get; init; }
    }

    private sealed class AiStyleResponse
    {
        [JsonPropertyName("matches")] public List<string> Matches { get; init; } = new();
    }
}
