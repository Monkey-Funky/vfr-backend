using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Mappings;

namespace Application.Features.Customer.Outfits.Queries.GetOutfitDetail;

/// <summary>
/// Returns full detail for a single outfit.
/// Cache-aside: TTL 10 minutes. Invalidated by UpdateOutfit and DeleteOutfit.
/// </summary>
internal sealed class GetOutfitDetailQueryHandler : IRequestHandler<GetOutfitDetailQuery, OutfitDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOutfitDetailQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<OutfitDetailDto> Handle(GetOutfitDetailQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view outfit details.");

        string cacheKey = $"outfit:{customerId:N}:{request.OutfitId:N}";

        var cached = await _cacheService.GetAsync<OutfitDetailDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var outfit = await _context.CustomerOutfits
            .AsNoTracking()
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.OutfitId, cancellationToken)
            ?? throw new NotFoundException("Outfit", request.OutfitId);

        if (outfit.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to view this outfit.");

        var productIds = outfit.Items.Select(i => i.ProductId).Distinct().ToList();

        // Parallelise product + inventory fetches
        var productsTask = _context.Products.AsNoTracking()
            .Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var inventoryTask = _context.InventoryRecords.AsNoTracking()
            .Where(ir => productIds.Contains(ir.ProductId))
            .ToDictionaryAsync(ir => ir.ProductId, cancellationToken);

        await Task.WhenAll(productsTask, inventoryTask);

        var productDict = (await productsTask).ToDictionary(p => p.Id);
        var inventoryDict = await inventoryTask;

        var dto = outfit.ToOutfitDetailDto(productDict, inventoryDict);

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return dto;
    }
}
