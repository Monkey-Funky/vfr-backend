using Application.Features.Settings.DTOs;

namespace Application.Features.Settings.Queries.GetNotificationPreferences;

/// <summary>
/// Returns the notification preferences for the currently authenticated retailer.
/// </summary>
public sealed record GetNotificationPreferencesQuery : IRequest<NotificationPreferenceDto>;