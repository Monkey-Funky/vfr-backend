using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Customer.Wardrobe.Commands.AddItemToCollection;

internal sealed class AddItemToCollectionCommandHandler : IRequestHandler<AddItemToCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public AddItemToCollectionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(AddItemToCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can modify collections.");

        // IDOR guard: verify the collection belongs to this customer.
        var collection = await _context.WardrobeCollections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException("WardrobeCollection", request.CollectionId);

        if (collection.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to access this collection.");

        // Verify the product is active.
        var product = await _context.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.RetailerId, p.Status })
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (product.Status != ProductStatus.Active)
            throw new NotFoundException("Product", request.ProductId);

        // Auto-create or restore the CustomerFavorite (adding to a collection implicitly favorites).
        var favorite = await _context.CustomerFavorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                f => f.CustomerId == customerId && f.ProductId == request.ProductId,
                cancellationToken);

        if (favorite is null)
        {
            favorite = CustomerFavorite.Create(customerId, request.ProductId, product.RetailerId);
            _context.CustomerFavorites.Add(favorite);
        }
        else if (favorite.IsDeleted)
        {
            favorite.Restore();
        }

        var collectionItem = WardrobeCollectionItem.Create(request.CollectionId, favorite.Id);
        _context.WardrobeCollectionItems.Add(collectionItem);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate the collection detail and the collections list cache.
            await Task.WhenAll(
                _cacheService.RemoveAsync($"wardrobe:{customerId:N}", cancellationToken),
                _cacheService.RemoveAsync(
                    $"wardrobe_items:{customerId:N}:{request.CollectionId:N}:p1s20", cancellationToken)
            );
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Idempotency: duplicate insert due to partial unique index — silently ignore.
        }
    }
}
