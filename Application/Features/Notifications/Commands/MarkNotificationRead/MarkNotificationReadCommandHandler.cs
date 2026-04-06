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

        Notification? notification = await _unitOfWork
            .Repository<Notification>()
            .GetByIdAsync(command.NotificationId, cancellationToken);

        if (notification is null)
            throw new NotFoundException(nameof(Notification), command.NotificationId);

        // IDOR guard — ensure the notification belongs to the current retailer.
        if (notification.RetailerId != retailerId)
            throw new UnauthorizedException("Access to this notification is forbidden.");

        // Idempotent — already-read notification returns success without re-saving.
        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _cacheService.RemoveByPrefixAsync($"notifications:{retailerId}:");
        }

        return Result<bool>.Success(true);
    }
}