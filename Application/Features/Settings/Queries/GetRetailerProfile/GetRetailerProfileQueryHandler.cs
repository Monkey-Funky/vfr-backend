using Application.Features.Settings.DTOs;
using Application.Features.Settings.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Settings.Queries.GetRetailerProfile;

/// <summary>
/// Loads the authenticated retailer's full profile from the database.
/// Uses <c>AsNoTracking()</c> — this is a read-only query.
/// Caches the result per-retailer for 5 minutes.
/// </summary>
public sealed class GetRetailerProfileQueryHandler
    : IRequestHandler<GetRetailerProfileQuery, RetailerSettingsProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetRetailerProfileQueryHandler> _logger;

    public GetRetailerProfileQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<GetRetailerProfileQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RetailerSettingsProfileDto> Handle(
        GetRetailerProfileQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Cache-Aside read ───────────────────────────────────────────────
        string cacheKey = $"profile:{retailerId:N}";

        RetailerSettingsProfileDto? cached =
            await _cacheService.GetAsync<RetailerSettingsProfileDto>(
                cacheKey, cancellationToken);

        if (cached is not null)
        {
            _logger.LogDebug(
                "GetRetailerProfile — cache hit. RetailerId: {RetailerId}", retailerId);
            return cached;
        }

        // ── 2. Cache miss — query database ────────────────────────────────────
        RetailerAccount? account = await _context.RetailerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == retailerId && !r.IsDeleted,
                cancellationToken);

        if (account is null)
        {
            _logger.LogWarning(
                "GetRetailerProfile — account not found. RetailerId: {RetailerId}", retailerId);
            throw new NotFoundException(nameof(RetailerAccount), retailerId);
        }

        RetailerSettingsProfileDto dto = account.ToSettingsProfileDto();

        // ── 3. Populate cache (5-minute TTL) ─────────────────────────────────
        await _cacheService.SetAsync(
            cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}
