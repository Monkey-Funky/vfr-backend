
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Application.Features.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler
    : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;

    public CreateCategoryCommandHandler(
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

    public async Task<Result<Guid>> Handle(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Layer 2 validation: name must be unique per retailer (partial index guard)
        bool nameExists = await _unitOfWork.Repository<Category>().AnyAsync(
            c => c.RetailerId == retailerId
                 && c.Name.ToLower() == command.Name.Trim().ToLower(),
            cancellationToken);

        if (nameExists)
            throw new ConflictException(nameof(Category), "Name", command.Name);

        // Upload cover image to S3 before creating the entity
        string coverImageUrl = await _fileStorageService.UploadAsync(
            command.CoverImageStream,
            command.CoverImageFileName,
            "category-covers",
            cancellationToken);

        Category category = Category.Create(
            retailerId: retailerId,
            name: command.Name,
            description: command.Description,
            coverImageUrl: coverImageUrl,
            status: command.Status);

        try
        {
            await _unitOfWork.Repository<Category>().AddAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // BUG-002 FIX: A concurrent request already inserted the same name.
            // Clean up the orphaned S3 image before converting to a conflict error.
            await _fileStorageService.DeleteAsync(coverImageUrl, cancellationToken);
            throw new ConflictException(nameof(Category), "Name", command.Name);
        }
        catch
        {
            // BUG-002 FIX: Any other DB failure — clean up the orphaned S3 image.
            await _fileStorageService.DeleteAsync(coverImageUrl, cancellationToken);
            throw;
        }

        // Invalidate category list cache for this retailer
        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<Guid>.Success(category.Id, "Category created successfully.");
    }
}