

namespace Application.Features.Inventory.Queries.ExportInventoryCsv;

/// <summary>
/// Streams all non-deleted inventory records for the authenticated retailer
/// into a UTF-8 encoded CSV byte array.
///
/// CANCELLATION NOTE:
///   The controller MUST pass HttpContext.RequestAborted as the CancellationToken.
///   This ensures the EF Core DataReader is disposed when the client disconnects.
///
/// STREAMING:
///   The handler uses IAsyncEnumerable + WithCancellation — rows are fetched
///   one at a time from PostgreSQL rather than materialised into memory at once.
///   This avoids OOM on large datasets.
/// </summary>
public sealed record ExportInventoryCsvQuery : IRequest<byte[]>;