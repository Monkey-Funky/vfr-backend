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

    public AddItemToCollectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(AddItemToCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId;

        // Check collection ownership
        var collection = await _context.WardrobeCollections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException("WardrobeCollection", request.CollectionId);

        if (collection.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this collection.");
        }

        // Check if product is active
        var product = await _context.Products
            .AsNoTracking()
            .Select(p => new { p.Id, p.RetailerId, p.Status })
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        if (product.Status != ProductStatus.Active)
            throw new NotFoundException("Product", request.ProductId);

        // Atomic Operation Required. Fetch or auto-create the CustomerFavorite
        var favorite = await _context.CustomerFavorites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.ProductId == request.ProductId, cancellationToken);

        if (favorite == null)
        {
            favorite = CustomerFavorite.Create((Guid)customerId, request.ProductId, product.RetailerId);
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
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Ignore duplicate insert due to partial unique index
        }
    }
}
