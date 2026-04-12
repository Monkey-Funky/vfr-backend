
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler
    : IRequestHandler<UpdateCategoryCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;

    public UpdateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: load category and verify ownership
        Category category = await _unitOfWork.Repository<Category>().FirstOrDefaultAsync(
            c => c.Id == command.CategoryId && c.RetailerId == retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        // Name uniqueness: exclude self from check
        if (command.NewName is not null)
        {
            string trimmedName = command.NewName.Trim().ToLower();

            bool nameConflict = await _unitOfWork.Repository<Category>().AnyAsync(
                c => c.RetailerId == retailerId
                     && c.Id != command.CategoryId
                     && c.Name.ToLower() == trimmedName,
                cancellationToken);

            if (nameConflict)
                throw new ConflictException(nameof(Category), "Name", command.NewName);
        }

        string? oldCoverImageUrl = null;
        string? newCoverImageUrl = null;

        if (command.NewCoverImageStream is not null
            && command.NewCoverImageFileName is not null)
        {
            oldCoverImageUrl = category.CoverImageUrl;

            newCoverImageUrl = await _fileStorageService.UploadAsync(
                command.NewCoverImageStream,
                command.NewCoverImageFileName,
                "category-covers",
                cancellationToken);
        }

        category.Update(
            newName: command.NewName,
            newDescription: command.NewDescription,
            shouldUpdateDescription: command.ShouldUpdateDescription,
            newCoverImageUrl: newCoverImageUrl,
            newStatus: command.Status);

        try
        {
            await _unitOfWork.Repository<Category>().UpdateAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (newCoverImageUrl is not null)
                await _fileStorageService.DeleteAsync(newCoverImageUrl, cancellationToken);
            throw new ConflictException(nameof(Category), "Name", command.NewName!);
        }
        catch
        {
            if (newCoverImageUrl is not null)
                await _fileStorageService.DeleteAsync(newCoverImageUrl, cancellationToken);
            throw;
        }

        // Delete old cover image from S3 only after a successful DB write
        if (oldCoverImageUrl is not null)
            await _fileStorageService.DeleteAsync(oldCoverImageUrl, cancellationToken);

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, "Category updated successfully.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint") == true
        || ex.InnerException?.Message.Contains("unique_violation") == true;
}