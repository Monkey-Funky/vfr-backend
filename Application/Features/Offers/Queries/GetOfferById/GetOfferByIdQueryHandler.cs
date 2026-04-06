using Application.Features.Offers.DTOs;
using Application.Features.Offers.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Offers.Queries.GetOfferById;

public sealed class GetOfferByIdQueryHandler
    : IRequestHandler<GetOfferByIdQuery, OfferDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetOfferByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<OfferDto> Handle(
        GetOfferByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: the predicate includes retailerId — a mismatched retailer receives 404
        Offer offer = await _context.Offers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Id == query.OfferId && o.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Offer), query.OfferId);

        return offer.ToDto();
    }
}