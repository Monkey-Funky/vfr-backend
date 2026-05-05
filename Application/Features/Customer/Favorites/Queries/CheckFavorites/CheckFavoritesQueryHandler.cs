using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Favorites.Queries.CheckFavorites;

internal sealed class CheckFavoritesQueryHandler : IRequestHandler<CheckFavoritesQuery, Dictionary<Guid, bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CheckFavoritesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Dictionary<Guid, bool>> Handle(CheckFavoritesQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId;

        // Ensure we always return a result for every requested ID
        var result = request.ProductIds.Distinct().ToDictionary(id => id, _ => false);

        if (customerId == null || request.ProductIds.Length == 0)
        {
            return result;
        }

        var favoritedIds = await _context.CustomerFavorites
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId && request.ProductIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var id in favoritedIds)
        {
            result[id] = true;
        }

        return result;
    }
}
