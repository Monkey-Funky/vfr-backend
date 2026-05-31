using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Settings.Commands.UpdateNotificationPreferences;

/// <summary>
/// Applies a partial update to the retailer's notification preferences record.
/// Only non-null fields in the command are written — this is strict PATCH semantics.
///
/// If the preference record does not exist (edge case: account pre-dates the preference
/// seeding fix in RegisterStep2), this handler creates a default record and applies
/// the requested changes on top of the defaults.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandHandler
    : IRequestHandler<UpdateNotificationPreferencesCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UpdateNotificationPreferencesCommandHandler> _logger;

    public UpdateNotificationPreferencesCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<UpdateNotificationPreferencesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Load preference record ─────────────────────────────────────────
        NotificationPreference? preference = await _unitOfWork
            .Repository<NotificationPreference>()
            .FirstOrDefaultAsync(
                p => p.RetailerId == retailerId,
                cancellationToken);

        if (preference is null)
        {
            // Edge case: account was created before preference seeding was introduced.
            // Create a default record and immediately apply the requested changes.
            _logger.LogWarning(
                "UpdateNotificationPreferences — preference record not found, seeding default. " +
                "RetailerId: {RetailerId}", retailerId);

            preference = NotificationPreference.CreateDefault(retailerId);
            await _unitOfWork.Repository<NotificationPreference>()
                .AddAsync(preference, cancellationToken);
        }

        // ── 2. Apply PATCH (only non-null fields) ─────────────────────────────
        preference.Update(
            lowStockAlerts: command.LowStockAlerts,
            orderStatusAlerts: command.OrderStatusAlerts,
            subscriptionAlerts: command.SubscriptionAlerts,
            emailNotifications: command.EmailNotifications,
            inAppNotifications: command.InAppNotifications);

        // ── 3. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<NotificationPreference>()
            .UpdateAsync(preference, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cached notification preferences for this retailer
        await _cacheService.RemoveAsync($"notif_prefs:{retailerId:N}", cancellationToken);

        _logger.LogInformation(
            "UpdateNotificationPreferences — preferences updated. RetailerId: {RetailerId}",
            retailerId);

        return Result<bool>.Success(true, "Notification preferences updated successfully.");
    }
}