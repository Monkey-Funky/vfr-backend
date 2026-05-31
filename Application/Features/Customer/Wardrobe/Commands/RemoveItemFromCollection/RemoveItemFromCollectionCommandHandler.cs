using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;

internal sealed class RemoveItemFromCollectionCommandHandler : IRequestHandler<RemoveItemFromCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public RemoveItemFromCollectionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(RemoveItemFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can modify collections.");

        // IDOR guard: verify the collection belongs to this customer.
        var collectionExists = await _context.WardrobeCollections
            .AnyAsync(c => c.Id == request.CollectionId && c.CustomerId == customerId, cancellationToken);

        if (!collectionExists)
            throw new NotFoundException("WardrobeCollection", request.CollectionId);

        // Find the specific collection-item link via the product ID.
        var collectionItem = await _context.WardrobeCollectionItems
            .Join(
                _context.CustomerFavorites,
                item => item.FavoriteId,
                fav => fav.Id,
                (item, fav) => new { Item = item, fav.ProductId })
            .Where(x => x.Item.CollectionId == request.CollectionId && x.ProductId == request.ProductId)
            .Select(x => x.Item)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Product in Collection", request.ProductId);

        collectionItem.SoftDelete();
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the collection items cache (all pages) and the collections list.
        await Task.WhenAll(
            _cacheService.RemoveAsync($"wardrobe:{customerId:N}", cancellationToken),
            _cacheService.RemoveByPrefixAsync(
                $"wardrobe_items:{customerId:N}:{request.CollectionId:N}:", cancellationToken)
        );
    }
}
