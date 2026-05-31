using Shared.Constants;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;

namespace Application.Features.Notifications.Commands.DeleteNotification;

/// <summary>
/// Hard-deletes a single notification belonging to the authenticated retailer.
/// Hard delete per spec — notification audit records have no business value after dismissal.
/// IDOR guard: notification.RetailerId must match the JWT retailer.
/// </summary>
public sealed class DeleteNotificationCommandHandler
    : IRequestHandler<DeleteNotificationCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteNotificationCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result> Handle(
        DeleteNotificationCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var notification = await _unitOfWork.Repository<Notification>()
            .GetByIdAsync(command.NotificationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), command.NotificationId);

        // IDOR guard
        if (notification.RetailerId != retailerId)
            throw new NotFoundException(nameof(Notification), command.NotificationId);

        await _unitOfWork.Repository<Notification>()
            .DeleteAsync(notification, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await Task.WhenAll(
            _cacheService.RemoveByPrefixAsync($"notifications:{retailerId}:", cancellationToken),
            _cacheService.RemoveAsync(CacheKeys.UnreadNotificationCount(retailerId), cancellationToken)
        );

        return Result.Success("Notification deleted.");
    }
}