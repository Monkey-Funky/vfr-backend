namespace Application.Interfaces.Services;

/// <summary>
/// In-process queue (backed by System.Threading.Channels) that transfers
/// pending Report IDs from GenerateReportCommandHandler to ReportGenerationJob.
///
/// Registered as Singleton — both the command handler and the BackgroundService
/// share the same Channel instance.
/// </summary>
public interface IReportQueue
{
    /// <summary>Enqueues a report ID for background processing.</summary>
    Task EnqueueAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads report IDs as they arrive. Blocks asynchronously until a new ID is queued.
    /// Consumed exclusively by ReportGenerationJob.
    /// </summary>
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}