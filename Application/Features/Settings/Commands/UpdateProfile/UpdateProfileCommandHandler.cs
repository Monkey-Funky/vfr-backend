using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Settings.Commands.UpdateProfile;

/// <summary>
/// Applies a partial profile update for the authenticated retailer.
///
/// BrandName uniqueness guard: if a new BrandName is supplied, the handler verifies
/// no OTHER non-deleted account uses the same brand name before persisting.
/// The self-exclusion predicate (<c>retailer_id != @self</c>) prevents a false
/// conflict when the retailer submits their own current brand name unchanged.
///
/// After saving, the profile cache entry is invalidated so the next GET
/// reflects the updated data immediately.
/// </summary>
public sealed class UpdateProfileCommandHandler
    : IRequestHandler<UpdateProfileCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        UpdateProfileCommand command,
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

        // ── 2. BrandName uniqueness check (self-excluded) ─────────────────────
        //
        // Only run if the caller is changing the brand name.
        // Predicate: brand_name = @newName AND retailer_id != @self AND is_deleted = false
        // This prevents a false-conflict when the retailer re-submits their own brand name.
        if (command.BrandName is not null)
        {
            string normalizedBrandName = command.BrandName.Trim().ToLowerInvariant();

            bool brandNameTaken = await _unitOfWork
                .Repository<RetailerAccount>()
                .AnyAsync(
                    r => r.BrandName.ToLower() == normalizedBrandName
                      && r.Id != retailerId
                      && !r.IsDeleted,
                    cancellationToken);

            if (brandNameTaken)
            {
                _logger.LogWarning(
                    "UpdateProfile — BrandName conflict. RetailerId: {RetailerId} | AttemptedBrandName: {BrandName}",
                    retailerId, command.BrandName);

                throw new ConflictException(
                    $"The brand name '{command.BrandName}' is already taken by another retailer.");
            }
        }

        // ── 3. Apply partial update (PATCH semantics) ─────────────────────────
        account.UpdateProfile(
            fullName: command.FullName,
            phoneNumber: command.PhoneNumber,
            brandName: command.BrandName,
            businessType: command.BusinessType);

        // ── 4. Persist ────────────────────────────────────────────────────────
        await _unitOfWork.Repository<RetailerAccount>().UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── 5. Invalidate profile cache ───────────────────────────────────────
        await _cacheService.RemoveAsync($"profile:{retailerId:N}", cancellationToken);

        _logger.LogInformation(
            "UpdateProfile — profile updated. RetailerId: {RetailerId}", retailerId);

        return Result<bool>.Success(true, "Profile updated successfully.");
    }
}