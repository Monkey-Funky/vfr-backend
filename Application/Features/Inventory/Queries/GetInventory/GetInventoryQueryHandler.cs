using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Inventory.Queries.GetInventory;


/// <summary>
/// Handles GetInventoryQuery — returns paginated, filtered inventory records.
///
/// Cache-aside pattern:
///   • Cache key encodes all filter/sort/page parameters plus the retailer ID.
///   • TTL = 5 minutes (inventory data changes on every stock adjustment).
///   • Cache is invalidated by AdjustStockCommandHandler and DeleteInventoryRecordCommandHandler.
/// </summary>
public sealed class GetInventoryQueryHandler
    : IRequestHandler<GetInventoryQuery, PagedResult<InventoryDto>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetInventoryQueryHandler(
        IInventoryRepository inventoryRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _inventoryRepository = inventoryRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<InventoryDto>> Handle(
        GetInventoryQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Cache-Aside ────────────────────────────────────────────────────────
        string cacheKey =
            $"inventory:{retailerId:N}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":name{query.ProductName ?? "null"}" +
            $":sold{query.SortBySoldQuantityDesc}";

        var cached = await _cacheService
            .GetAsync<PagedResult<InventoryDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        // ── Database Query ─────────────────────────────────────────────────────
        var (items, totalCount) = await _inventoryRepository.GetPagedAsync(
            retailerId: retailerId,
            productNameFilter: query.ProductName,
            sortBySoldQuantityDesc: query.SortBySoldQuantityDesc,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        var result = new PagedResult<InventoryDto>
        {
            Items = items.Select(r => r.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
