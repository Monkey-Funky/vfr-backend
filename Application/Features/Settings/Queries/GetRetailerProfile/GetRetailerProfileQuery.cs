using Application.Features.Settings.DTOs;

namespace Application.Features.Settings.Queries.GetRetailerProfile;

/// <summary>
/// Returns the full profile of the currently authenticated retailer.
/// RetailerId is resolved from <see cref="ICurrentUserService"/> — it is never passed
/// as a query parameter.
/// </summary>
public sealed record GetRetailerProfileQuery : IRequest<RetailerSettingsProfileDto>;