using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Mappings;

namespace Application.Features.Customer.Outfits.Queries.GetOutfits;

internal sealed class GetOutfitsQueryHandler : IRequestHandler<GetOutfitsQuery, PagedResult<OutfitSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOutfitsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<OutfitSummaryDto>> Handle(GetOutfitsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view outfits.");

        var outfits = await _context.CustomerOutfits
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items.Where(i => !i.IsDeleted))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        // Fetch primary images for each product referenced by these outfits
        var productIds = outfits
            .SelectMany(o => o.Items)
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

        var items = outfits.Select(o => o.ToOutfitSummaryDto(productsImages)).ToList();

        // Wrap in PagedResult so the response shape is consistent with other list
        // endpoints and integration-test assertions can deserialise correctly.
        // All outfits are returned as a single page (no server-side pagination needed
        // for personal outfit collections which are naturally bounded in size).
        return new PagedResult<OutfitSummaryDto>
        {
            Items = items,
            PageNumber = 1,
            PageSize = items.Count > 0 ? items.Count : 1,
            TotalCount = items.Count
        };
    }
}