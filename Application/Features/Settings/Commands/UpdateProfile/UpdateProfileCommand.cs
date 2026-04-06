
namespace Application.Features.Settings.Commands.UpdateProfile;

/// <summary>
/// Updates the profile of the currently authenticated retailer.
/// All fields are optional (PATCH semantics) — only non-null values are applied.
/// Email changes are NOT supported in this flow (separate verification flow).
/// </summary>
/// <param name="FullName">New full name (max 100 chars), or null to leave unchanged.</param>
/// <param name="PhoneNumber">New phone number (max 20 chars), or null to leave unchanged.</param>
/// <param name="BrandName">
///   New brand name (max 150 chars), or null to leave unchanged.
///   Uniqueness is enforced in the handler (self-update excluded from check).
/// </param>
/// <param name="BusinessType">New business type (max 50 chars), or null to leave unchanged.</param>
public sealed record UpdateProfileCommand(
    string? FullName,
    string? PhoneNumber,
    string? BrandName,
    string? BusinessType) : IRequest<Result<bool>>;