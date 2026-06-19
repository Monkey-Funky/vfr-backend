namespace Domain.Entities.Customer;

/// <summary>
/// Persistent deduplication cache for paid fal.ai generation requests.
/// Prevents repeated charges for identical avatar / try-on inputs.
/// </summary>
public sealed class AiGenerationCache
{
    public Guid Id { get; private set; }

    /// <summary>The customer who triggered this generation (for per-customer quota tracking).</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>SHA-256 hex string uniquely identifying the generation inputs.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>Avatar3D | TryOn3D | TryOn2D</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Processing | Completed | Failed</summary>
    public string Status { get; private set; } = AiGenerationStatus.Processing;

    public string Provider { get; private set; } = string.Empty;
    public string ModelId { get; private set; } = string.Empty;
    public string PipelineVersion { get; private set; } = string.Empty;

    /// <summary>Serialised input parameters (hashes/URLs only — never raw bytes).</summary>
    public string InputJson { get; private set; } = string.Empty;

    public string? ResultJson { get; private set; }
    public string? ResultImageUrl { get; private set; }
    public string? ResultModelUrl { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }

    private AiGenerationCache() { }

    public static AiGenerationCache CreateProcessing(
        Guid customerId,
        string requestHash,
        string type,
        string provider,
        string modelId,
        string pipelineVersion,
        string inputJson)
    {
        return new AiGenerationCache
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            RequestHash = requestHash,
            Type = type,
            Status = AiGenerationStatus.Processing,
            Provider = provider,
            ModelId = modelId,
            PipelineVersion = pipelineVersion,
            InputJson = inputJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkCompleted(string? resultImageUrl, string? resultModelUrl, string? resultJson = null)
    {
        Status = AiGenerationStatus.Completed;
        ResultImageUrl = resultImageUrl;
        ResultModelUrl = resultModelUrl;
        ResultJson = resultJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorCode, string errorMessage)
    {
        Status = AiGenerationStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        FailedAt = DateTime.UtcNow;
    }
}

public static class AiGenerationStatus
{
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class AiGenerationType
{
    public const string Avatar3D = "Avatar3D";
    public const string TryOn3D = "TryOn3D";
    public const string TryOn2D = "TryOn2D";
}
