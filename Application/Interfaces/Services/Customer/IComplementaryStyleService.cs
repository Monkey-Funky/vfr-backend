namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Talks to the external AI model to find products that pair well with a given product.
/// </summary>
public interface IComplementaryStyleService
{
    /// <summary>
    /// Returns a list of AI-generated matching Product Identifiers (e.g., SKUs or AI IDs).
    /// </summary>
    Task<List<string>> GetComplementaryItemsAsync(string productAiId, int topK, CancellationToken ct = default);
    /// <summary>
    /// Returns a list of AI-generated similar Product Identifiers (e.g., SKUs or AI IDs).
    /// </summary>
    Task<List<string>> GetSimilarItemsAsync(string modelId, int topK, CancellationToken ct = default);

}