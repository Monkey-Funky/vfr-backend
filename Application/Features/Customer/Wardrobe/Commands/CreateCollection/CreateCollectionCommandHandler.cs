using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Customer.Wardrobe.Commands.CreateCollection;

internal sealed class CreateCollectionCommandHandler : IRequestHandler<CreateCollectionCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public CreateCollectionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Guid> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can create collections.");

        var collection = WardrobeCollection.Create(customerId, request.Name);
        _context.WardrobeCollections.Add(collection);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate the collections list cache so the new collection appears immediately.
            await _cacheService.RemoveAsync($"wardrobe:{customerId:N}", cancellationToken);

            return collection.Id;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            throw new ConflictException("A collection with this name already exists.");
        }
    }
}
