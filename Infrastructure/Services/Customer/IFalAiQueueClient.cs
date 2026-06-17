namespace Infrastructure.Services.Customer;

/// <summary>
/// Reusable client for the fal.ai async queue pattern: Submit → Poll → Fetch.
/// Shared by the 3D SAM pipeline (<see cref="FalAiService"/>) and the 2D try-on
/// service so both speak to <c>queue.fal.run</c> the same way. Authentication,
/// base URL and poll cadence come from the shared "FalAi" settings.
/// </summary>
public interface IFalAiQueueClient
{
    /// <summary>
    /// Submits <paramref name="requestBody"/> to the fal.ai app <paramref name="apiId"/>,
    /// polls until completion, then deserializes the response into <typeparamref name="TResult"/>.
    /// </summary>
    /// <param name="apiId">fal.ai app id, e.g. "fal-ai/sam-3/3d-body" or "fal-ai/fashn/tryon".</param>
    /// <param name="maxPollSeconds">Optional override for the completion timeout; falls back to the shared setting.</param>
    Task<TResult> SubmitAndPollAsync<TRequest, TResult>(
        string apiId,
        TRequest requestBody,
        CancellationToken ct,
        int? maxPollSeconds = null);
}
