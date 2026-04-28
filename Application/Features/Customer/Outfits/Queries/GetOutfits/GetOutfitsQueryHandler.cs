using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Mappings;

namespace Application.Features.Customer.Outfits.Queries.GetOutfits;

internal sealed class GetOutfitsQueryHandler : IRequestHandler<GetOutfitsQuery, List<OutfitSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOutfitsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<OutfitSummaryDto>> Handle(GetOutfitsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view outfits.");

        var outfits = await _context.CustomerOutfits
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Fetch products associated with these items to get PrimaryImageUrl
        var productIds = outfits.SelectMany(o => o.Items)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var productsImages = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                PrimaryImage = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id, p => p.PrimaryImage, cancellationToken);

        var result = outfits.Select(o => o.ToOutfitSummaryDto(productsImages)).ToList();

        return result;
    }
}
