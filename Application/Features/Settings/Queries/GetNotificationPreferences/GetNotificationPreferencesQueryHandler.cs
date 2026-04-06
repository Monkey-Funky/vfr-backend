using Application.Features.Settings.DTOs;
using Application.Features.Settings.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Settings.Queries.GetNotificationPreferences;

/// <summary>
/// Loads the authenticated retailer's notification preferences.
/// Uses <c>AsNoTracking()</c> — read-only query.
/// </summary>
public sealed class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferenceDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetNotificationPreferencesQueryHandler> _logger;

    public GetNotificationPreferencesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<GetNotificationPreferencesQueryHandler> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<NotificationPreferenceDto> Handle(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

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

        return preference.ToDto();
    }
}