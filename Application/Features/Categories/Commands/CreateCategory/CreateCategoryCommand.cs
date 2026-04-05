
namespace Application.Features.Categories.Commands.CreateCategory;


/// <summary>
/// Creates a new Category for the authenticated retailer.
/// RetailerId is resolved from <see cref="ICurrentUserService"/> — never supplied by the caller.
///
/// NOTE: The cover image is carried as a Stream (opened by the API controller from IFormFile).
/// The handler uploads it to S3 via <see cref="IFileStorageService"/> and stores the URL.
/// </summary>
public sealed record CreateCategoryCommand(
    string Name,
    string? Description,
    Stream CoverImageStream,
    string CoverImageFileName,
    string CoverImageContentType,
    string Status
) : IRequest<Result<Guid>>;