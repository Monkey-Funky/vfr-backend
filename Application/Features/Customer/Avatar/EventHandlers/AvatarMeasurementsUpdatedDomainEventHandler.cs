using Application.Interfaces.Persistence;
using Domain.Entities.Customer;
using Domain.Events.Customer;

namespace Application.Features.Customer.Avatar.EventHandlers;

public sealed class AvatarMeasurementsUpdatedDomainEventHandler
    : INotificationHandler<AvatarMeasurementsUpdatedDomainEvent>
{
    private readonly IApplicationDbContext _context;

    public AvatarMeasurementsUpdatedDomainEventHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task Handle(AvatarMeasurementsUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: notification.AvatarId,
            measurementDataJson: notification.MeasurementDataJson,
            source: notification.Source);

        _context.AvatarMeasurementHistory.Add(history);
        
        return Task.CompletedTask;
    }
}
