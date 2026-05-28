using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;

public sealed class UpdateAvatarMeasurementsCommandHandler : IRequestHandler<UpdateAvatarMeasurementsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateAvatarMeasurementsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateAvatarMeasurementsCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        var avatar = await _context.Avatars
            .Include(a => a.MeasurementHistories)
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

        // The domain entity owns the measurement update AND the history snapshot.
        // UpdateMeasurements appends the snapshot to _measurementHistories; EF Core
        // detects the addition through the ".WithMany("_measurementHistories")" relationship
        // configuration and inserts the new row in the same transaction as the avatar UPDATE.
        avatar.UpdateMeasurements(measurements, request.Source);

        await _context.SaveChangesAsync(cancellationToken);
    }
}