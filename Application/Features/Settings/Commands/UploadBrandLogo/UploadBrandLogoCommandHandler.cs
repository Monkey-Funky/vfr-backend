using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Settings.Commands.UploadBrandLogo;

/// <summary>
/// Handles brand logo upload with the atomic replace-or-create pattern:
/// deletes the old logo from S3 first, uploads the new one, then persists the new URL.
/// </summary>
public sealed class UploadBrandLogoCommandHandler
    : IRequestHandler<UploadBrandLogoCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UploadBrandLogoCommandHandler> _logger;

    private const string BrandLogoFolder = "brand-logos";

    public UploadBrandLogoCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService,
        ILogger<UploadBrandLogoCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(
        UploadBrandLogoCommand command,
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

        // ── 2. Delete old brand logo from S3 before uploading new one ─────────
        if (!string.IsNullOrEmpty(account.BrandLogoUrl))
        {
            try
            {
                await _fileStorageService.DeleteAsync(account.BrandLogoUrl, cancellationToken);

                _logger.LogInformation(
                    "UploadBrandLogo — old logo deleted. RetailerId: {RetailerId} | OldUrl: {Url}",
                    retailerId, account.BrandLogoUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "UploadBrandLogo — failed to delete old logo (continuing). " +
                    "RetailerId: {RetailerId} | OldUrl: {Url}",
                    retailerId, account.BrandLogoUrl);
            }
        }

        // ── 3. Upload new logo to S3 ──────────────────────────────────────────
        await using MemoryStream fileStream = new(command.FileContent);

        string newLogoUrl = await _fileStorageService.UploadAsync(
            stream: fileStream,
            fileName: command.FileName,
            folder: BrandLogoFolder,
            ct: cancellationToken);

        _logger.LogInformation(
            "UploadBrandLogo — new logo uploaded. RetailerId: {RetailerId} | NewUrl: {Url}",
            retailerId, newLogoUrl);

        // ── 4. Persist new URL ────────────────────────────────────────────────
        account.SetBrandLogoUrl(newLogoUrl);

        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 5. Invalidate profile cache ───────────────────────────────────────
        await _cacheService.RemoveAsync($"profile:{retailerId:N}", cancellationToken);

        return Result<string>.Success(newLogoUrl, "Brand logo uploaded successfully.");
    }
}
