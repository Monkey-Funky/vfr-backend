using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Commands.RegisterStep2;

/// <summary>
/// Second and final step of two-step retailer registration.
///
/// The client supplies the step token received from RegisterStep1 along with
/// business details and an optional brand logo file.
///
/// On success:
///   • The RetailerAccount transitions from PendingEmailVerification to Active
///   • A NotificationPreference record is seeded atomically in the same transaction
///   • A verification email is sent fire-and-forget
///   • Full auth tokens (access + refresh) are returned
///
/// Note on file handling:
///   Application layer uses Stream instead of IFormFile to avoid a dependency
///   on Microsoft.AspNetCore.Http. The API controller converts IFormFile to Stream
///   before dispatching this command.
/// </summary>
public sealed record RegisterStep2Command(
    /// <summary>
    /// The step token returned by RegisterStep1Command.
    /// Contains the partial account's ID and a step=1 claim.
    /// Expires in 15 minutes.
    /// </summary>
    string TempStepToken,

    /// <summary>Business category (e.g. "Fashion", "Electronics"). Max 50 chars.</summary>
    string BusinessType,

    /// <summary>Whether the retailer already has 3D product models ready for upload.</summary>
    bool Has3DModels,

    /// <summary>
    /// Readable stream of the brand logo file. Null if no logo is provided.
    /// The controller must NOT dispose this stream before the handler completes.
    /// </summary>
    Stream? BrandLogoStream,

    /// <summary>Original file name of the logo including extension (e.g. "logo.png").</summary>
    string? BrandLogoFileName,

    /// <summary>MIME content type of the logo (e.g. "image/png", "image/jpeg").</summary>
    string? BrandLogoContentType,

    /// <summary>File size in bytes. Used by the validator for the 2 MB size check.</summary>
    long BrandLogoSizeBytes

) : IRequest<Result<AuthTokenResponse>>;