using Application.Features.Customer.Avatar.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Queries.GetSizeRecommendation;

public sealed class GetSizeRecommendationQueryHandler : IRequestHandler<GetSizeRecommendationQuery, SizeRecommendationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISizeRecommendationService _sizeRecommendationService;

    public GetSizeRecommendationQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISizeRecommendationService sizeRecommendationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sizeRecommendationService = sizeRecommendationService;
    }

    public async Task<SizeRecommendationDto> Handle(GetSizeRecommendationQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        var avatar = await _context.Avatars
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CustomerId == customerId, cancellationToken);

        if (avatar is null)
            throw new NotFoundException("Avatar", customerId);

        // Real implementation would ensure product exists, but for scope we trust the SizeRecommendationService
        return await _sizeRecommendationService.RecommendSizeAsync(avatar, request.ProductId, cancellationToken);
    }
}