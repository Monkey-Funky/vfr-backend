using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Commands.DeleteCollection;

internal sealed class DeleteCollectionCommandHandler : IRequestHandler<DeleteCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteCollectionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can delete collections.");

        // IDOR guard: verify ownership in the same query.
        var collection = await _context.WardrobeCollections
            .FirstOrDefaultAsync(
                c => c.Id == request.CollectionId && c.CustomerId == customerId,
                cancellationToken)
            ?? throw new NotFoundException("WardrobeCollection", request.CollectionId);

        collection.SoftDelete();

        // Cascade soft-delete all items in the collection.
        var items = await _context.WardrobeCollectionItems
            .Where(i => i.CollectionId == request.CollectionId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            item.SoftDelete();

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the collections list cache for this customer.
        await _cacheService.RemoveAsync($"wardrobe:{customerId:N}", cancellationToken);
    }
}
