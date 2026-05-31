using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.DeleteOutfit;

internal sealed class DeleteOutfitCommandHandler : IRequestHandler<DeleteOutfitCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteOutfitCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task Handle(DeleteOutfitCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can delete outfits.");

        // IDOR guard: load with ownership check in a single query.
        var outfit = await _context.CustomerOutfits
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OutfitId, cancellationToken)
            ?? throw new NotFoundException("Outfit", request.OutfitId);

        if (outfit.CustomerId != customerId)
            throw new UnauthorizedAccessException("You are not authorized to delete this outfit.");

        // Domain method cascades soft-delete to all items.
        outfit.SoftDelete();

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate both the detail cache and the list cache for this customer.
        await Task.WhenAll(
            _cacheService.RemoveAsync($"outfit:{customerId:N}:{request.OutfitId:N}", cancellationToken),
            _cacheService.RemoveAsync($"outfits:{customerId:N}", cancellationToken)
        );
    }
}
