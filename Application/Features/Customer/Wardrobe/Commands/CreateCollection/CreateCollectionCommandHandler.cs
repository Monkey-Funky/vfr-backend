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

    public CreateCollectionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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
            return collection.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            throw new ConflictException("A collection with this name already exists.");
        }
    }
}
