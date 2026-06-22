using Application.Common;
using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

public sealed record ExtractMeasurementsFromImageCommand(
    FileUploadDto FrontImageFile,
    FileUploadDto? SideImageFile,
    decimal HeightCm
) : IRequest<AvatarDto>;
