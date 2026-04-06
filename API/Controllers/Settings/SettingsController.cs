using Application.Features.Settings.Commands.ChangePassword;
using Application.Features.Settings.Commands.DeleteAccount;
using Application.Features.Settings.Commands.DeleteAvatar;
using Application.Features.Settings.Commands.DeleteBrandLogo;
using Application.Features.Settings.Commands.UpdateNotificationPreferences;
using Application.Features.Settings.Commands.UpdateProfile;
using Application.Features.Settings.Commands.UploadAvatar;
using Application.Features.Settings.Commands.UploadBrandLogo;
using Application.Features.Settings.DTOs;
using Application.Features.Settings.Queries.GetNotificationPreferences;
using Application.Features.Settings.Queries.GetRetailerProfile;
using Microsoft.IdentityModel.Tokens.Experimental;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Settings;

/// <summary>
/// Handles all Profile and Settings endpoints for an authenticated retailer.
/// Every endpoint is retailer-scoped and protected by the IDOR guard in <c>BaseApiController</c>.
/// </summary>
[SwaggerTag("Retailer Profile & Settings")]
[Route("api/retailers/{retailerId:guid}")]
public sealed class SettingsController : BaseApiController
{
    // =========================================================================
    // GET — Profile
    // =========================================================================

    /// <summary>
    /// Returns the full profile of the authenticated retailer.
    /// </summary>
    [HttpGet("profile", Name = "GetRetailerProfile")]
    [SwaggerOperation(
        Summary = "Get retailer profile",
        Description = "Returns the full profile fields for the authenticated retailer. " +
                      "Excludes all sensitive fields (password hash, refresh token, lockout state).")]
    [ProducesResponseType(typeof(ApiResponse<RetailerSettingsProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        RetailerSettingsProfileDto profile =
            await Sender.Send(new GetRetailerProfileQuery(), cancellationToken);

        return OkResponse(profile);
    }

    // =========================================================================
    // PUT — Update Profile
    // =========================================================================

    /// <summary>
    /// Partially updates the authenticated retailer's profile (PATCH semantics).
    /// </summary>
    [HttpPut("profile")]
    [SwaggerOperation(
        Summary = "Update retailer profile",
        Description = "Updates any combination of FullName, PhoneNumber, BrandName, and BusinessType. " +
                      "All fields are optional — only supplied (non-null) fields are written. " +
                      "Email changes are handled through a separate verification flow.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid retailerId,
        [FromBody] UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // PUT — Change Password
    // =========================================================================

    /// <summary>
    /// Changes the retailer's password and revokes all active sessions.
    /// </summary>
    [HttpPut("profile/password")]
    [SwaggerOperation(
        Summary = "Change password",
        Description = "Verifies the current password, sets the new password, and revokes " +
                      "ALL active refresh tokens — forcing re-login on every device. " +
                      "The client must discard its current tokens after this call succeeds.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword(
        [FromRoute] Guid retailerId,
        [FromBody] ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST — Upload Avatar
    // =========================================================================

    /// <summary>
    /// Uploads or replaces the retailer's personal avatar image.
    /// </summary>
    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload avatar",
        Description = "Uploads a new avatar image. Accepted formats: JPEG, PNG, GIF, WebP. " +
                      "Maximum file size: 5 MB. " +
                      "If an avatar already exists, it is deleted from storage before the new one is uploaded.")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadAvatar(
        [FromRoute] Guid retailerId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        // Read the file into a byte[] in the API layer so that the Application layer
        // stays free of ASP.NET Core abstractions (IFormFile is an HTTP concern).
        byte[] fileContent = await ReadFormFileAsync(file, cancellationToken);

        UploadAvatarCommand command = new(
            FileContent: fileContent,
            FileName: file.FileName);

        Result<string> result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE — Delete Avatar
    // =========================================================================

    /// <summary>
    /// Removes the retailer's avatar from storage and clears the avatar URL.
    /// </summary>
    [HttpDelete("profile/avatar")]
    [SwaggerOperation(
        Summary = "Delete avatar",
        Description = "Deletes the current avatar from blob storage and clears the AvatarUrl field. " +
                      "Returns 200 even if no avatar is set (idempotent).")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAvatar(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(new DeleteAvatarCommand(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST — Upload Brand Logo
    // =========================================================================

    /// <summary>
    /// Uploads or replaces the retailer's brand logo.
    /// </summary>
    [HttpPost("profile/brand-logo")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Upload brand logo",
        Description = "Uploads a new brand logo. Accepted formats: JPEG, PNG, GIF, WebP. " +
                      "Maximum file size: 5 MB. " +
                      "If a logo already exists, it is deleted from storage before the new one is uploaded.")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadBrandLogo(
        [FromRoute] Guid retailerId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        byte[] fileContent = await ReadFormFileAsync(file, cancellationToken);

        UploadBrandLogoCommand command = new(
            FileContent: fileContent,
            FileName: file.FileName);

        Result<string> result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE — Delete Brand Logo
    // =========================================================================

    /// <summary>
    /// Removes the retailer's brand logo from storage and clears the logo URL.
    /// </summary>
    [HttpDelete("profile/brand-logo")]
    [SwaggerOperation(
        Summary = "Delete brand logo",
        Description = "Deletes the current brand logo from blob storage and clears the BrandLogoUrl field. " +
                      "Returns 200 even if no logo is set (idempotent).")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteBrandLogo(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(new DeleteBrandLogoCommand(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // GET — Notification Preferences
    // =========================================================================

    /// <summary>
    /// Returns the notification preferences for the authenticated retailer.
    /// </summary>
    [HttpGet("settings/notifications", Name = "GetNotificationPreferences")]
    [SwaggerOperation(
        Summary = "Get notification preferences",
        Description = "Returns all notification preference flags for the retailer.")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotificationPreferences(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        NotificationPreferenceDto preferences =
            await Sender.Send(new GetNotificationPreferencesQuery(), cancellationToken);

        return OkResponse(preferences);
    }

    // =========================================================================
    // PATCH — Update Notification Preferences
    // =========================================================================

    /// <summary>
    /// Partially updates the retailer's notification preferences.
    /// </summary>
    [HttpPatch("settings/notifications")]
    [SwaggerOperation(
        Summary = "Update notification preferences",
        Description = "Updates one or more notification preference flags (PATCH semantics). " +
                      "Omit any field to leave it unchanged. " +
                      "At least one field must be provided.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateNotificationPreferences(
        [FromRoute] Guid retailerId,
        [FromBody] UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE — Account (GDPR)
    // =========================================================================

    /// <summary>
    /// Initiates GDPR account deletion. Sets account to PendingDeletion and revokes all sessions.
    /// </summary>
    [HttpDelete("account")]
    [SwaggerOperation(
        Summary = "Delete account (GDPR)",
        Description = "Marks the retailer account as PendingDeletion, revokes all active sessions " +
                      "immediately, and schedules permanent PII erasure after a 30-day grace period. " +
                      "A confirmation email is sent to the retailer's address. " +
                      "This operation is IDEMPOTENT: calling it again when already PendingDeletion " +
                      "returns 200 without creating a second event.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAccount(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(new DeleteAccountCommand(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // Private Helpers
    // =========================================================================

    /// <summary>
    /// Reads an <c>IFormFile</c> into a <c>byte[]</c>.
    /// Validates that the file is present and non-empty at the HTTP boundary.
    /// The Application layer validator then enforces size and magic-byte constraints.
    /// </summary>
    private static async Task<byte[]> ReadFormFileAsync(
    IFormFile? file,
    CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new Domain.Exceptions.ValidationException(
                new Dictionary<string, string[]>
                {
                { "File", new[] { "A file must be provided and must not be empty." } }
                });

        await using MemoryStream memoryStream = new();
        await file.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }
}