using Application.Interfaces.Services;
using System.Runtime.CompilerServices;
using System.Threading.Channels;


namespace Infrastructure.Services.Dashboard;

/// <summary>
/// In-process queue backed by System.Threading.Channels.
/// Registered as Singleton so that GenerateReportCommandHandler and
/// ReportGenerationJob share the same channel instance.
///
/// The channel is bounded (capacity = 1000) to provide back-pressure:
/// if 1000 reports are queued and not yet processed, EnqueueAsync
/// will wait until a slot becomes available.
/// </summary>
public sealed class ReportQueue : IReportQueue
{
    private readonly Channel<Guid> _channel;

    public ReportQueue()
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // only ReportGenerationJob reads
            SingleWriter = false  // multiple HTTP requests may enqueue concurrently
        });
    }

    public async Task EnqueueAsync(Guid reportId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(reportId, cancellationToken);

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (Guid reportId in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return reportId;
    }
}