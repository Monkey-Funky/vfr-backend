using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.DeleteAvatar;

/// <summary>
/// Deletes the avatar file from S3 and clears the AvatarUrl on the entity.
/// If no avatar is set, returns success immediately (idempotent).
/// </summary>
public sealed class DeleteAvatarCommandHandler
    : IRequestHandler<DeleteAvatarCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeleteAvatarCommandHandler> _logger;

    public DeleteAvatarCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<DeleteAvatarCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        DeleteAvatarCommand command,
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

        // ── 2. No-op if avatar not set ────────────────────────────────────────
        if (string.IsNullOrEmpty(account.AvatarUrl))
        {
            _logger.LogDebug(
                "DeleteAvatar — no avatar set, no-op. RetailerId: {RetailerId}", retailerId);

            return Result<bool>.Success(true, "No avatar to delete.");
        }

        // ── 3. Delete from S3 ─────────────────────────────────────────────────
        await _fileStorageService.DeleteAsync(account.AvatarUrl, cancellationToken);

        _logger.LogInformation(
            "DeleteAvatar — avatar deleted from S3. RetailerId: {RetailerId} | Url: {Url}",
            retailerId, account.AvatarUrl);

        // ── 4. Clear the URL and persist ──────────────────────────────────────
        account.SetAvatarUrl(null);

        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 5. Invalidate profile cache ───────────────────────────────────────
        await _cacheService.RemoveAsync($"profile:{retailerId:N}", cancellationToken);

        return Result<bool>.Success(true, "Avatar deleted successfully.");
    }
}