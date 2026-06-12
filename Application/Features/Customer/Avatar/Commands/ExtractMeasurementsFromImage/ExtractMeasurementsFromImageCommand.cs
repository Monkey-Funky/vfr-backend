using Application.Common;
using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

/// <summary>
/// Sends a customer's full-body photo to an external AI model to extract
/// body measurements and upserts the customer's avatar.
///
/// • If no avatar exists → creates one with source = "ai_image".
/// • If an avatar already exists → updates measurements with source = "ai_image".
/// • The image is ephemeral — it is NOT persisted to storage after extraction.
/// </summary>
public sealed record ExtractMeasurementsFromImageCommand(
    FileUploadDto ImageFile,
    decimal HeightCm
) : IRequest<AvatarDto>;
