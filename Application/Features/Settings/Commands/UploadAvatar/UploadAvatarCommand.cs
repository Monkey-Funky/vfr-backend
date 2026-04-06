

namespace Application.Features.Settings.Commands.UploadAvatar;

/// <summary>
/// Uploads a new avatar image for the currently authenticated retailer.
///
/// The controller reads the <c>IFormFile</c> into a <c>byte[]</c> before constructing
/// this command, keeping <c>IFormFile</c> out of the Application layer.
///
/// The old avatar (if any) is deleted from S3 BEFORE the new one is uploaded
/// to prevent orphaned files accumulating in the bucket.
/// </summary>
/// <param name="FileContent">Raw bytes of the image file. Max 5 MB.</param>
/// <param name="FileName">Original file name including extension (e.g. "avatar.png").</param>
public sealed record UploadAvatarCommand(
    byte[] FileContent,
    string FileName) : IRequest<Result<string>>;