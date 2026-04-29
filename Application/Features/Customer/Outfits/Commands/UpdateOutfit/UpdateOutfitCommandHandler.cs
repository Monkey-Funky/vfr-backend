using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.UpdateOutfit;

internal sealed class UpdateOutfitCommandHandler : IRequestHandler<UpdateOutfitCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateOutfitCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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
        {
            throw new UnauthorizedAccessException("You are not authorized to update this outfit.");
        }

        // SECURITY MANDATE: Verify all requested products are favorited by this customer
        var requestedProductIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        
        var favoritedProductIds = await _context.CustomerFavorites
            .Where(f => f.CustomerId == customerId && requestedProductIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        var missingProductIds = requestedProductIds.Except(favoritedProductIds).ToList();
        
        if (missingProductIds.Any())
        {
            throw new BusinessRuleException(
                "INVALID_OUTFIT_ITEMS", 
                $"Cannot update outfit. The following products must be favorited first: {string.Join(", ", missingProductIds)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            outfit.Rename(request.Name);
        }
        
        outfit.UpdateStyle(request.StyleCategory);

        outfit.ClearItems();

        foreach (var item in request.Items)
        {
            outfit.AddOrUpdateItem(item.ProductId, item.SlotType, item.DisplayOrder);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
