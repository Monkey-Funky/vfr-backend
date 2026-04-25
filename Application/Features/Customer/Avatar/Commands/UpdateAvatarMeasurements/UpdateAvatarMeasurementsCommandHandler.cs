using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;

public sealed class UpdateAvatarMeasurementsCommandHandler : IRequestHandler<UpdateAvatarMeasurementsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public UpdateAvatarMeasurementsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task Handle(UpdateAvatarMeasurementsCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var avatar = await _context.Avatars
            .FirstOrDefaultAsync(a => a.Id == request.AvatarId, cancellationToken);

        if (avatar is null || avatar.CustomerId != customerId)
            throw new NotFoundException("Avatar", request.AvatarId);

        var measurements = new BodyMeasurements(
            request.HeightCm,
            request.WeightKg,
            request.ChestCm,
            request.WaistCm,
            request.HipsCm,
            request.ShoulderWidthCm,
            request.InseamCm,
            request.NeckCm,
            request.ArmLengthCm,
            request.ShoeSizeEu,
            request.BodyShape
        );

        avatar.UpdateMeasurements(measurements, request.Source);

        foreach (var domainEvent in avatar.DomainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
        avatar.DomainEvents.Clear();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
