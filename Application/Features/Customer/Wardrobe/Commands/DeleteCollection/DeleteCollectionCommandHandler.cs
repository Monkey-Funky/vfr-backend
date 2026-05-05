using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Commands.DeleteCollection;

internal sealed class DeleteCollectionCommandHandler : IRequestHandler<DeleteCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCollectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can delete collections.");

        var collection = await _context.WardrobeCollections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundException("WardrobeCollection", request.CollectionId);

        if (collection is null)
        {
            throw new NotFoundException("WardrobeCollection", request.CollectionId);
        }

        collection.SoftDelete();

        // Also soft-delete all items in the collection
        var items = await _context.WardrobeCollectionItems
            .Where(i => i.CollectionId == request.CollectionId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.SoftDelete();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
