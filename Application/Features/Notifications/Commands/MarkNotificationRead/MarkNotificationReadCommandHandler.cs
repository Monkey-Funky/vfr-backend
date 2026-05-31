using Shared.Constants;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
namespace Application.Features.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public MarkNotificationReadCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        MarkNotificationReadCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var notification = await _unitOfWork.Repository<Notification>()
            .GetByIdAsync(command.NotificationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), command.NotificationId);

        // IDOR: notification must belong to the authenticated retailer
        if (notification.RetailerId != retailerId)
            throw new NotFoundException(nameof(Notification), command.NotificationId);

        // Idempotent — no-op if already read
        notification.MarkAsRead(); // sets IsRead = true, ReadAt = UtcNow

        await _unitOfWork.Repository<Notification>()
            .UpdateAsync(notification, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await Task.WhenAll(
            _cacheService.RemoveByPrefixAsync($"notifications:{retailerId}:", cancellationToken),
            _cacheService.RemoveAsync(CacheKeys.UnreadNotificationCount(retailerId), cancellationToken)
        );

        return Result<bool>.Success(true, "Notification marked as read.");
    }
}