
namespace Application.Features.Settings.Commands.UploadBrandLogo;

/// <summary>
/// Uploads a new brand logo for the currently authenticated retailer.
/// Follows the same replace-or-create pattern as <see cref="UploadAvatarCommand"/>:
/// the old logo is deleted from S3 before the new one is uploaded.
/// </summary>
/// <param name="FileContent">Raw bytes of the logo image. Max 5 MB.</param>
/// <param name="FileName">Original file name including extension (e.g. "logo.png").</param>
public sealed record UploadBrandLogoCommand(
    byte[] FileContent,
    string FileName) : IRequest<Result<string>>;
