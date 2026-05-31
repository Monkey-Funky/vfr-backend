using Application.Features.Settings.DTOs;
using Application.Features.Settings.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Settings.Queries.GetNotificationPreferences;

/// <summary>
/// Loads the authenticated retailer's notification preferences.
/// Cache-aside: TTL 15 minutes. Invalidated by UpdateNotificationPreferences command.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferenceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetNotificationPreferencesQueryHandler> _logger;

    public GetNotificationPreferencesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<GetNotificationPreferencesQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<NotificationPreferenceDto> Handle(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        string cacheKey = $"notif_prefs:{retailerId:N}";

        var cached = await _cacheService.GetAsync<NotificationPreferenceDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("GetNotificationPreferences — cache hit. RetailerId: {RetailerId}", retailerId);
            return cached;
        }

        NotificationPreference? preference = await _context.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.RetailerId == retailerId,
                cancellationToken);

        if (preference is null)
        {
            _logger.LogWarning(
                "GetNotificationPreferences — preference record not found. RetailerId: {RetailerId}",
                retailerId);
            throw new NotFoundException(nameof(NotificationPreference), retailerId);
        }

        var dto = preference.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15), cancellationToken);

        return dto;
    }
}
