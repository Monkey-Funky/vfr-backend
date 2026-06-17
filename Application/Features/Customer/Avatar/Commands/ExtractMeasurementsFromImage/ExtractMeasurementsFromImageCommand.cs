using Application.Common;
using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

/// <summary>
/// Sends a customer's full-body photo to an external AI model to extract
/// body measurements and upserts the customer's avatar.
///
/// • If no avatar exists → creates one with source = "ai_image".
/// • If an avatar already exists → updates measurements with source = "ai_image".
/// • The front image is uploaded to persistent storage and kept as the avatar's
///   source image — it is required later for both 2D Overlay try-on and, when
///   available, the 3D SAM Align step. The side image is used for measurement
///   extraction only and is not persisted.
/// </summary>
public sealed record ExtractMeasurementsFromImageCommand(
    FileUploadDto FrontImageFile,
    FileUploadDto SideImageFile,
    decimal HeightCm
) : IRequest<AvatarDto>;
