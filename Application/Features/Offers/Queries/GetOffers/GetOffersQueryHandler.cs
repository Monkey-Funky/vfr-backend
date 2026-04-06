using Application.Features.Offers.DTOs;
using Application.Features.Offers.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Offers.Queries.GetOffers;

public sealed class GetOffersQueryHandler
    : IRequestHandler<GetOffersQuery, PagedResult<OfferDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOffersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<OfferDto>> Handle(
        GetOffersQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Cache-Aside (TTL: 2 min — offers are time-sensitive)
        string cacheKey =
            $"offers:{retailerId}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":status{query.Status ?? "null"}" +
            $":type{query.OfferType ?? "null"}";

        PagedResult<OfferDto>? cached =
            await _cacheService.GetAsync<PagedResult<OfferDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        IQueryable<Offer> queryable = _context.Offers
            .AsNoTracking()
            .Where(o => o.RetailerId == retailerId);

        if (query.Status is not null)
            queryable = queryable.Where(o => o.Status == query.Status);

        if (query.OfferType is not null)
            queryable = queryable.Where(o => o.OfferType == query.OfferType);

        int totalCount = await queryable.CountAsync(cancellationToken);

        List<Offer> offers = await queryable
            .OrderByDescending(o => o.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        PagedResult<OfferDto> result = offers.ToPagedDto(
            totalCount, query.PageNumber, query.PageSize);

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}