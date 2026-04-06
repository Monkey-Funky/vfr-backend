using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.UploadAvatar;

/// <summary>
/// Handles avatar upload with an atomic replace-or-create pattern:
///   1. Load the current account and note the old avatar URL.
///   2. Delete the old avatar from S3 (if it exists) — prevents orphan files.
///   3. Upload the new file to S3 and retrieve the public URL.
///   4. Persist the new URL on the entity and save.
///   5. Invalidate the profile cache.
///
/// If the S3 delete fails (transient error), the upload is still attempted.
/// An orphan file is preferable to blocking the update entirely.
/// </summary>
public sealed class UploadAvatarCommandHandler
    : IRequestHandler<UploadAvatarCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UploadAvatarCommandHandler> _logger;

    /// <summary>Logical folder / key prefix for avatar files in the S3 bucket.</summary>
    private const string AvatarFolder = "avatars";

    public UploadAvatarCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<UploadAvatarCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(
        UploadAvatarCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException(
                "Retailer identity could not be resolved from the access token.");

        // ── 1. Load the retailer account ──────────────────────────────────────
        RetailerAccount? account = await _unitOfWork
            .Repository<RetailerAccount>()
            .FirstOrDefaultAsync(
                r => r.Id == retailerId && !r.IsDeleted,
                cancellationToken);

        if (account is null)
            throw new NotFoundException(nameof(RetailerAccount), retailerId);

        // ── 2. Delete old avatar from S3 before uploading the new one ─────────
        //
        // Best-effort: if the delete fails (e.g. file already gone), we log and continue.
        // An orphan file in S3 is a minor cost; blocking the retailer's update is not.
        if (!string.IsNullOrEmpty(account.AvatarUrl))
        {
            try
            {
                await _fileStorageService.DeleteAsync(account.AvatarUrl, cancellationToken);

                _logger.LogInformation(
                    "UploadAvatar — old avatar deleted. RetailerId: {RetailerId} | OldUrl: {Url}",
                    retailerId, account.AvatarUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "UploadAvatar — failed to delete old avatar (continuing). " +
                    "RetailerId: {RetailerId} | OldUrl: {Url}",
                    retailerId, account.AvatarUrl);
            }
        }

        // ── 3. Upload new avatar to S3 ────────────────────────────────────────
        await using MemoryStream fileStream = new(command.FileContent);

        string newAvatarUrl = await _fileStorageService.UploadAsync(
            stream: fileStream,
            fileName: command.FileName,
            folder: AvatarFolder,
            ct: cancellationToken);

        _logger.LogInformation(
            "UploadAvatar — new avatar uploaded. RetailerId: {RetailerId} | NewUrl: {Url}",
            retailerId, newAvatarUrl);

        // ── 4. Persist new URL ────────────────────────────────────────────────
        account.SetAvatarUrl(newAvatarUrl);

        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 5. Invalidate profile cache ───────────────────────────────────────
        await _cacheService.RemoveAsync($"profile:{retailerId:N}", cancellationToken);

        return Result<string>.Success(newAvatarUrl, "Avatar uploaded successfully.");
    }
}