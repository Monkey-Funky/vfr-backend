

namespace Application.Features.Settings.Commands.DeleteBrandLogo;

/// <summary>
/// Deletes the current brand logo from S3 and clears the <c>BrandLogoUrl</c> field.
/// No-op if no brand logo is set (returns 200 without error).
/// </summary>
public sealed record DeleteBrandLogoCommand : IRequest<Result<bool>>;