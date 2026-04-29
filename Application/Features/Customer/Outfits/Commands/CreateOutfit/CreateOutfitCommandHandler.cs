using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.CreateOutfit;

internal sealed class CreateOutfitCommandHandler : IRequestHandler<CreateOutfitCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateOutfitCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateOutfitCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedAccessException("Only authenticated customers can create outfits.");

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
                $"Cannot create outfit. The following products must be favorited first: {string.Join(", ", missingProductIds)}");
        }

        // 2. THE SILENT MERGE (Duplicate Detection)
        // Load the user's existing outfits and their active items
        var existingOutfits = await _context.CustomerOutfits
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        // Find an outfit that has the EXACT same item count, 
        // where every incoming item matches an existing item in both ProductId and SlotType.
        var duplicateOutfit = existingOutfits.FirstOrDefault(o =>
            o.Items.Count == request.Items.Count &&
            request.Items.All(reqItem => o.Items.Any(existingItem =>
                existingItem.ProductId == reqItem.ProductId &&
                existingItem.SlotType == reqItem.SlotType))
        );

        // If an exact match exists, silently return its ID. 
        // The UI receives a 200 OK success, but the database writes nothing!
        if (duplicateOutfit != null)
        {
            return duplicateOutfit.Id;
        }

        // 3. CREATE NEW OUTFIT (If no duplicate was found)
        var outfit = CustomerOutfit.Create(customerId, request.Name, request.StyleCategory);

        foreach (var item in request.Items)
        {
            outfit.AddOrUpdateItem(item.ProductId, item.SlotType, item.DisplayOrder);
        }

        _context.CustomerOutfits.Add(outfit);
        await _context.SaveChangesAsync(cancellationToken);

        return outfit.Id;
    }
}
