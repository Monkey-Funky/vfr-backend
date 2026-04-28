using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Mappings;

namespace Application.Features.Customer.Outfits.Queries.GetOutfitDetail;

internal sealed class GetOutfitDetailQueryHandler : IRequestHandler<GetOutfitDetailQuery, OutfitDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOutfitDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<OutfitDetailDto> Handle(GetOutfitDetailQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view outfit details.");

        var outfit = await _context.CustomerOutfits
            .AsNoTracking()
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.OutfitId, cancellationToken)
            ?? throw new NotFoundException("Outfit", request.OutfitId);

        if (outfit.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this outfit.");
        }

        var productIds = outfit.Items.Select(i => i.ProductId).Distinct().ToList();

        // Cross-reference Products table for full details. 
        // We handle soft-deleted/inactive products by checking status.
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var productDict = products.ToDictionary(p => p.Id);

        return outfit.ToOutfitDetailDto(productDict);
    }
}
