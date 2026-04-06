using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.DeleteBrandLogo;

/// <summary>
/// Deletes the brand logo file from S3 and clears the BrandLogoUrl on the entity.
/// If no logo is set, returns success immediately (idempotent).
/// </summary>
public sealed class DeleteBrandLogoCommandHandler
    : IRequestHandler<DeleteBrandLogoCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeleteBrandLogoCommandHandler> _logger;

    public DeleteBrandLogoCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<DeleteBrandLogoCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteBrandLogoCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Load account ───────────────────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Id == retailerId && !r.IsDeleted,
                cancellationToken);

        if (account is null)
            throw new NotFoundException(nameof(RetailerAccount), retailerId);

        // ── 2. No-op if brand logo not set ────────────────────────────────────
        if (string.IsNullOrEmpty(account.BrandLogoUrl))
        {
            _logger.LogDebug(
                "DeleteBrandLogo — no logo set, no-op. RetailerId: {RetailerId}", retailerId);

            return Result<bool>.Success(true, "No brand logo to delete.");
        }

        // ── 3. Delete from S3 ─────────────────────────────────────────────────
        await _fileStorageService.DeleteAsync(account.BrandLogoUrl, cancellationToken);

        _logger.LogInformation(
            "DeleteBrandLogo — logo deleted from S3. RetailerId: {RetailerId} | Url: {Url}",
            retailerId, account.BrandLogoUrl);

        // ── 4. Clear the URL and persist ──────────────────────────────────────
        account.SetBrandLogoUrl(null);

        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 5. Invalidate profile cache ───────────────────────────────────────
        await _cacheService.RemoveAsync($"profile:{retailerId:N}", cancellationToken);

        return Result<bool>.Success(true, "Brand logo deleted successfully.");
    }
}