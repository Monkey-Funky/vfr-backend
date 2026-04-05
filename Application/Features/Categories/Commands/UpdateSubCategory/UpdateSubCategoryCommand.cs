namespace Application.Features.Categories.Commands.UpdateSubCategory;

/// <summary>Updates an existing SubCategory's Name and/or Status.</summary>
public sealed record UpdateSubCategoryCommand(
    Guid ParentCategoryId,  
    Guid SubCategoryId,
    string? NewName,
    string? Status
) : IRequest<Result<bool>>;