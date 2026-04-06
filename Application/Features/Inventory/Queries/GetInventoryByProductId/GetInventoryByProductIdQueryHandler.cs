using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Inventory.Queries.GetInventoryByProductId;

public sealed class GetInventoryByProductIdQueryHandler
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryDto>
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

    public async Task<InventoryDto> Handle(
        GetInventoryByProductIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Cache-Aside ────────────────────────────────────────────────────────
        string cacheKey = $"inventory:{retailerId:N}:product:{query.ProductId:N}";

        var cached = await _cacheService
            .GetAsync<InventoryDto>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        // ── Database Query ─────────────────────────────────────────────────────
        var record = await _inventoryRepository.GetByProductIdAsync(
            retailerId,
            query.ProductId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryRecord), query.ProductId);

        var dto = record.ToDto();

        await _cacheService.SetAsync(
            cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return dto;
    }
}
