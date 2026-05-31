using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Constants;

namespace Application.Features.Customer.Favorites.Commands.ToggleFavorite;

internal sealed class ToggleFavoriteCommandHandler : IRequestHandler<ToggleFavoriteCommand, (bool IsSuccess, bool IsFavoriteNow)>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public ToggleFavoriteCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<(bool IsSuccess, bool IsFavoriteNow)> Handle(ToggleFavoriteCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can toggle favorites.");

        // Validate product
        var product = await _context.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.RetailerId, p.Status })
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        // Find existing favorite (ignoring query filters to find soft-deleted ones)
        var existingFavorite = await _context.CustomerFavorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.ProductId == request.ProductId, cancellationToken);

        bool isFavoriteNow;

        if (existingFavorite != null)
        {
            if (!existingFavorite.IsDeleted)
            {
                // Unfavorite
                existingFavorite.SoftDelete();

                // Cascade: soft-delete all collection items referencing this favorite
                var orphanedItems = await _context.WardrobeCollectionItems
                    .Where(i => i.FavoriteId == existingFavorite.Id)
                    .ToListAsync(cancellationToken);

                foreach (var item in orphanedItems)
                    item.SoftDelete();

                isFavoriteNow = false;
            }
            else
            {
                if (product.Status != ProductStatus.Active)
                    throw new BusinessRuleException("ProductInactive", "Cannot favorite a discontinued product.");

                existingFavorite.Restore();
                isFavoriteNow = true;
            }
        }
        else
        {
            if (product.Status != ProductStatus.Active)
                throw new BusinessRuleException("ProductInactive", "Cannot favorite a discontinued product.");

            var newFavorite = CustomerFavorite.Create(customerId, request.ProductId, product.RetailerId);
            _context.CustomerFavorites.Add(newFavorite);
            isFavoriteNow = true;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate favorites cache for this customer (all pages)
            await _cacheService.RemoveByPrefixAsync(
                CacheKeys.CustomerFavoritesList(customerId), cancellationToken);

            return (true, isFavoriteNow);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Idempotency: rapid toggle race condition — duplicate already favorited.
            return (true, true);
        }
    }
}
