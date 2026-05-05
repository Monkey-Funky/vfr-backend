using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Customer.Favorites.Commands.ToggleFavorite;

internal sealed class ToggleFavoriteCommandHandler : IRequestHandler<ToggleFavoriteCommand, (bool IsSuccess, bool IsFavoriteNow)>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ToggleFavoriteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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
            return (true, isFavoriteNow);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Idempotency: Rapid toggle race condition. 
            // If we tried to insert and a duplicate exists, someone else beat us to it.
            return (true, true);
        }
    }
}
