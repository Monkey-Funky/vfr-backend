using Application.Features.Categories.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;

namespace Application.Features.Categories.Commands.ToggleCategoryStatus;


public sealed class ToggleCategoryStatusCommandHandler
    : IRequestHandler<ToggleCategoryStatusCommand, Result<CategoryStatusDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public ToggleCategoryStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<CategoryStatusDto>> Handle(
        ToggleCategoryStatusCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        Category category = await _unitOfWork.Repository<Category>().FirstOrDefaultAsync(
            c => c.Id == command.CategoryId && c.RetailerId == retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Category), command.CategoryId);

        string newStatus = category.ToggleStatus();

        await _unitOfWork.Repository<Category>().UpdateAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveByPrefixAsync($"categories:{retailerId}:", cancellationToken);

        return Result<CategoryStatusDto>.Success(
            new CategoryStatusDto(newStatus),
            $"Category status toggled to '{newStatus}'.");
    }
}