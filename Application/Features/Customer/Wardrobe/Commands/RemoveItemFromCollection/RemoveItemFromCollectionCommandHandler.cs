using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;

internal sealed class RemoveItemFromCollectionCommandHandler : IRequestHandler<RemoveItemFromCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RemoveItemFromCollectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(RemoveItemFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can modify collections.");

        // 1. Secure IDOR Check
        var collectionExists = await _context.WardrobeCollections
            .AnyAsync(c => c.Id == request.CollectionId && c.CustomerId == customerId, cancellationToken);

        if (!collectionExists)
            throw new NotFoundException("WardrobeCollection", request.CollectionId);

        // 2. The Join: Find the specific item link using the ProductId
        var collectionItem = await _context.WardrobeCollectionItems
            .Join(_context.CustomerFavorites,
                  item => item.FavoriteId,
                  fav => fav.Id,
                  (item, fav) => new { Item = item, fav.ProductId })
            .Where(x => x.Item.CollectionId == request.CollectionId && x.ProductId == request.ProductId)
            .Select(x => x.Item)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Product in Collection", request.ProductId);

        // 3. Domain Deletion
        collectionItem.SoftDelete();

        // 4. Save
        await _context.SaveChangesAsync(cancellationToken);
    }
}
