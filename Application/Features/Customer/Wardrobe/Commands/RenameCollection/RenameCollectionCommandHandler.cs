using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Customer.Wardrobe.Commands.RenameCollection;

internal sealed class RenameCollectionCommandHandler : IRequestHandler<RenameCollectionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public RenameCollectionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(RenameCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can rename collections.");

        var collection = await _context.WardrobeCollections
            .FirstOrDefaultAsync(
                c => c.Id == request.CollectionId && c.CustomerId == customerId,
                cancellationToken)
            ?? throw new NotFoundException("WardrobeCollection", request.CollectionId);

        collection.Rename(request.NewName);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate the collections list cache so the renamed entry is reflected.
            await _cacheService.RemoveAsync($"wardrobe:{customerId:N}", cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            throw new ConflictException("A collection with this name already exists.");
        }
    }
}
