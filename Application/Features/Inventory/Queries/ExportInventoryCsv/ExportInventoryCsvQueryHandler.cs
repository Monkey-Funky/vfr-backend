using Application.Features.Inventory.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using System.Text;


namespace Application.Features.Inventory.Queries.ExportInventoryCsv;

/// <summary>
/// Handles ExportInventoryCsvQuery — streams inventory records into a CSV byte array.
///
/// STREAMING PATTERN (same as ExportOrdersCsvQueryHandler in P-030):
///   • IInventoryRepository.StreamAsync() returns IAsyncEnumerable{InventoryRecord}.
///     The AsAsyncEnumerable() call lives in Infrastructure (relational EF provider).
///   • WithCancellation(ct) ensures the PostgreSQL DataReader is released immediately
///     if the HTTP client disconnects mid-download.
///   • cancellationToken.ThrowIfCancellationRequested() before each sb.AppendLine()
///     provides defence-in-depth.
/// </summary>
public sealed class ExportInventoryCsvQueryHandler
    : IRequestHandler<ExportInventoryCsvQuery, byte[]>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentUserService _currentUserService;

    public ExportInventoryCsvQueryHandler(
        IInventoryRepository inventoryRepository,
        ICurrentUserService currentUserService)
    {
        _inventoryRepository = inventoryRepository;
        _currentUserService = currentUserService;
    }

    public async Task<byte[]> Handle(
        ExportInventoryCsvQuery request,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var sb = new StringBuilder();

        // CSV header
        sb.AppendLine(
            "InventoryRecordId,ProductId,ProductName," +
            "CurrentStock,SoldQuantity,LowStockThreshold,Status,CreatedAt");

        await foreach (var record in _inventoryRepository
            .StreamAsync(retailerId, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            // Defence-in-depth: check before each write
            cancellationToken.ThrowIfCancellationRequested();

            sb.AppendLine(record.ToCsvRow().ToCsvLine());
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}