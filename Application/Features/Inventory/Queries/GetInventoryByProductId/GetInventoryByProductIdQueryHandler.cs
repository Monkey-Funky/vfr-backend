using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Inventory.Queries.GetInventoryByProductId;

public sealed class GetInventoryByProductIdQueryHandler
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryDetailDto>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetInventoryByProductIdQueryHandler(
        IInventoryRepository inventoryRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _inventoryRepository = inventoryRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<InventoryDetailDto> Handle(
        GetInventoryByProductIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Cache key includes a detail suffix so it never conflicts with the list cache
        string cacheKey = $"inventory:{retailerId:N}:product:{query.ProductId:N}:detail";

        var cached = await _cacheService
            .GetAsync<InventoryDetailDto>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        // GetByProductIdAsync loads the record WITH StockAdjustments (Include)
        var record = await _inventoryRepository.GetByProductIdAsync(
            retailerId,
            query.ProductId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryRecord), query.ProductId);

        // IDOR belt-and-suspenders (repository already filters by retailerId)
        if (record.RetailerId != retailerId)
            throw new NotFoundException(nameof(InventoryRecord), query.ProductId);

        var dto = record.ToDetailDto();

        await _cacheService.SetAsync(
            cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}