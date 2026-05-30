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

        // Update avatar properties (does NOT touch the navigation collection).
        avatar.UpdateMeasurements(measurements, request.Source);

        // Record history snapshot explicitly via the DbSet.
        // Adding through the navigation backing field causes a DbUpdateConcurrencyException
        // when the navigation was not eagerly loaded — EF Core cannot reliably detect
        // entities added to an unloaded navigation's backing field.
        var measurementJson = Domain.Entities.Customer.Avatar.BuildMeasurementJson(measurements);
        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementJson,
            source: request.Source);

        _context.AvatarMeasurementHistory.Add(history);

        await _context.SaveChangesAsync(cancellationToken);
    }
}