using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        avatar.UpdateMeasurements(measurements);

        // Record history snapshot in the same transaction — pure procedural CQRS.
        // The handler owns the side-effect, not the domain entity.
        var measurementsJson = JsonSerializer.Serialize(measurements);
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementsJson,
            source: request.Source);

        _context.AvatarMeasurementHistory.Add(history);

        await _context.SaveChangesAsync(cancellationToken);
    }
}

