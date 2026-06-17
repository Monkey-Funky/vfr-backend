using Application.Common;
using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Commands.RepairAvatarSourceImage;

/// <summary>
/// Re-uploads a new front-facing photo for an existing avatar whose
/// <c>SourceImageUrl</c> is <see langword="null"/> (typically because fal.ai
/// timed out during the original <c>extract-from-image</c> call and the old
/// code had the two-phase-save bug).
///
/// <para>
/// After a successful upload the handler sets <c>SourceImageUrl</c> and
/// optionally re-triggers 3D body-model generation, making both 2D and 3D
/// try-on available again without forcing the customer to re-submit their
/// measurements.
/// </para>
/// </summary>
public sealed record RepairAvatarSourceImageCommand(
    FileUploadDto FrontImageFile,

    /// <summary>
    /// When <see langword="true"/> the handler will attempt to re-generate
    /// the 3D body model via fal.ai after the upload succeeds.
    /// Set to <see langword="false"/> to only restore 2D try-on capability
    /// (faster, cheaper, avoids another potential fal.ai timeout).
    /// </summary>
    bool RetryGenerate3D = true
) : IRequest<AvatarDto>;
