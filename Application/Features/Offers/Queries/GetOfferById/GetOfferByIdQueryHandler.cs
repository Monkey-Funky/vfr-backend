using Application.Features.Offers.DTOs;
using Application.Features.Offers.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Offers.Queries.GetOfferById;

/// <summary>
/// Returns a single offer by ID for the authenticated retailer.
/// Cache-aside: TTL 15 minutes. Invalidated by UpdateOffer and DeleteOffer.
/// </summary>
public sealed class GetOfferByIdQueryHandler
    : IRequestHandler<GetOfferByIdQuery, OfferDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOfferByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<OfferDto> Handle(
        GetOfferByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = $"offer:{retailerId:N}:{query.OfferId:N}";

        var cached = await _cacheService.GetAsync<OfferDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        Offer offer = await _context.Offers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Id == query.OfferId && o.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Offer), query.OfferId);

        var dto = offer.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15), cancellationToken);

        return dto;
    }
}
