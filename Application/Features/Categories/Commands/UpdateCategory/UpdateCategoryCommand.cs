

namespace Application.Features.Categories.Commands.UpdateCategory;


/// <summary>
/// Updates an existing Category. All non-ID fields are optional.
/// Only fields that are non-null (for strings/stream) or true (for shouldUpdateDescription)
/// are applied. If <see cref="NewCoverImageStream"/> is provided, the old S3 image is deleted.
/// </summary>
public sealed record UpdateCategoryCommand(
    Guid CategoryId,
    string? NewName,
    string? NewDescription,
    bool ShouldUpdateDescription,
    Stream? NewCoverImageStream,
    string? NewCoverImageFileName,
    string? NewCoverImageContentType,
    string? Status
) : IRequest<Result<bool>>;