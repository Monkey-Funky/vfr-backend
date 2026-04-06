
namespace Application.Features.Settings.Commands.UploadAvatar;

/// <summary>
/// Validates the <see cref="UploadAvatarCommand"/>.
/// Checks file size and validates magic bytes (file signature) to prevent
/// content-type spoofing — a client cannot bypass this by lying about the extension.
///
/// Allowed formats: JPEG, PNG, GIF, WebP.
/// Maximum size: 5 MB.
/// </summary>
public sealed class UploadAvatarCommandValidator : AbstractValidator<UploadAvatarCommand>
{
    /// <summary>Maximum allowed file size: 5 MB in bytes.</summary>
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;

    public UploadAvatarCommandValidator()
    {
        RuleFor(x => x.FileContent)
            .NotNull().WithMessage("File content is required.")
            .NotEmpty().WithMessage("File content must not be empty.")
            .Must(content => content.Length <= MaxFileSizeBytes)
            .WithMessage("Avatar image must not exceed 5 MB.")
            .Must(HasValidImageMagicBytes)
            .WithMessage(
                "The uploaded file is not a valid image. " +
                "Accepted formats: JPEG, PNG, GIF, WebP.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(255).WithMessage("File name must not exceed 255 characters.");
    }

    /// <summary>
    /// Validates the first bytes of the content against known image format signatures.
    /// This check cannot be bypassed by changing the file extension or Content-Type header.
    /// </summary>
    private static bool HasValidImageMagicBytes(byte[]? content)
    {
        if (content is null || content.Length < 4)
            return false;

        // JPEG: FF D8 FF
        if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 (‰PNG)
        if (content[0] == 0x89 && content[1] == 0x50 &&
            content[2] == 0x4E && content[3] == 0x47)
            return true;

        // GIF: 47 49 46 38 (GIF8)
        if (content[0] == 0x47 && content[1] == 0x49 &&
            content[2] == 0x46 && content[3] == 0x38)
            return true;

        // WebP: 52 49 46 46 ?? ?? ?? ?? 57 45 42 50 (RIFF????WEBP)
        if (content.Length >= 12 &&
            content[0] == 0x52 && content[1] == 0x49 &&
            content[2] == 0x46 && content[3] == 0x46 &&
            content[8] == 0x57 && content[9] == 0x45 &&
            content[10] == 0x42 && content[11] == 0x50)
            return true;

        return false;
    }
}