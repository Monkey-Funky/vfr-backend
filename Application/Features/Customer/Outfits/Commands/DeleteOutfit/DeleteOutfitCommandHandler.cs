using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Outfits.Commands.DeleteOutfit;

internal sealed class DeleteOutfitCommandHandler : IRequestHandler<DeleteOutfitCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteOutfitCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeleteOutfitCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedAccessException("Only authenticated customers can delete outfits.");

        var outfit = await _context.CustomerOutfits
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OutfitId, cancellationToken)
            ?? throw new NotFoundException("Outfit", request.OutfitId);

        if (outfit.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this outfit.");
        }

        outfit.SoftDelete();
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}
