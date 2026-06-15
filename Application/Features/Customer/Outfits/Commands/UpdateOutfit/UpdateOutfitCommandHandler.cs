using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.UpdateOutfit;

internal sealed class UpdateOutfitCommandHandler : IRequestHandler<UpdateOutfitCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public UpdateOutfitCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(UpdateOutfitCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can update outfits.");

        var outfit = await _context.CustomerOutfits
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OutfitId, cancellationToken)
            ?? throw new NotFoundException("Outfit", request.OutfitId);

        if (outfit.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to update this outfit.");

        // SECURITY: every product in the updated outfit must be favorited by this customer.
        var requestedProductIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

        var favoritedProductIds = await _context.CustomerFavorites
            .Where(f => f.CustomerId == customerId && requestedProductIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        var missingProductIds = requestedProductIds.Except(favoritedProductIds).ToList();
        if (missingProductIds.Count > 0)
            throw new BusinessRuleException(
                "INVALID_OUTFIT_ITEMS",
                $"Cannot update outfit. The following products must be favorited first: {string.Join(", ", missingProductIds)}");

        if (!string.IsNullOrWhiteSpace(request.Name))
            outfit.Rename(request.Name);

        outfit.UpdateStyle(request.StyleCategory);

        // Snapshot the IDs already tracked by EF Core (items loaded via .Include above).
        // These are either Unchanged or will become Modified after ClearItems().
        var trackedItemIds = outfit.Items
            .Select(i => i.Id)
            .ToHashSet();

        // Soft-delete all existing active items via the domain method.
        outfit.ClearItems();

        // Re-add the desired items via the domain method.
        foreach (var item in request.Items)
            outfit.AddOrUpdateItem(item.ProductId, item.SlotType, item.DisplayOrder);

        // FIX: EF Core does NOT automatically track new entities added directly to a
        // private backing field (List<T>). Items created inside AddOrUpdateItem() via
        // CustomerOutfitItem.Create() are Detached — EF Core won't INSERT them.
        // We must explicitly register any item that EF Core has never seen before.
        foreach (var item in outfit.Items.Where(i => !i.IsDeleted))
        {
            if (!trackedItemIds.Contains(item.Id))
                _context.CustomerOutfitItems.Add(item);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate both the detail cache and the list cache for this customer.
        await Task.WhenAll(
            _cacheService.RemoveAsync($"outfit:{customerId:N}:{request.OutfitId:N}", cancellationToken),
            _cacheService.RemoveAsync($"outfits:{customerId:N}", cancellationToken)
        );
    }
}