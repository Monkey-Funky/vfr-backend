using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.CreateOutfit;

internal sealed class CreateOutfitCommandHandler : IRequestHandler<CreateOutfitCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public CreateOutfitCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Guid> Handle(CreateOutfitCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can create outfits.");

        // SECURITY: every product in the outfit must already be favorited by this customer.
        var requestedProductIds = request.Items.Select(i => i.ProductId).Distinct().ToList();

        var favoritedProductIds = await _context.CustomerFavorites
            .Where(f => f.CustomerId == customerId && requestedProductIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        var missingProductIds = requestedProductIds.Except(favoritedProductIds).ToList();
        if (missingProductIds.Count > 0)
            throw new BusinessRuleException(
                "INVALID_OUTFIT_ITEMS",
                $"Cannot create outfit. The following products must be favorited first: {string.Join(", ", missingProductIds)}");

        // Silent merge: if an identical outfit already exists, return its ID instead of creating a duplicate.
        var existingOutfits = await _context.CustomerOutfits
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var duplicateOutfit = existingOutfits.FirstOrDefault(o =>
            o.Items.Count == request.Items.Count &&
            request.Items.All(reqItem => o.Items.Any(existing =>
                existing.ProductId == reqItem.ProductId &&
                existing.SlotType == reqItem.SlotType)));

        if (duplicateOutfit is not null)
            return duplicateOutfit.Id;

        // Create the new outfit using the correct domain method: AddOrUpdateItem(productId, slot, displayOrder).
        var outfit = CustomerOutfit.Create(customerId, request.Name, request.StyleCategory);

        foreach (var item in request.Items)
            outfit.AddOrUpdateItem(item.ProductId, item.SlotType, item.DisplayOrder);

        _context.CustomerOutfits.Add(outfit);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the customer's outfit list cache.
        await _cacheService.RemoveAsync($"outfits:{customerId:N}", cancellationToken);

        return outfit.Id;
    }
}
