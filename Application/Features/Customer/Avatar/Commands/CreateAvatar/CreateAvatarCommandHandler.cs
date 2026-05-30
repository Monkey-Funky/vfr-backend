using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Avatar.Commands.CreateAvatar;

public sealed class CreateAvatarCommandHandler : IRequestHandler<CreateAvatarCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateAvatarCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateAvatarCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var existingExists = await _context.Avatars
            .AnyAsync(a => a.CustomerId == customerId, cancellationToken);

        if (existingExists)
            throw new BusinessRuleException("AVATAR_EXISTS", "This customer already has an active avatar. Use Update instead.");

        var avatar = Domain.Entities.Customer.Avatar.Create(
            customerId: customerId,
            heightCm: request.HeightCm,
            weightKg: request.WeightKg,
            chestCm: request.ChestCm,
            waistCm: request.WaistCm,
            hipsCm: request.HipsCm,
            shoulderWidthCm: request.ShoulderWidthCm,
            inseamCm: request.InseamCm,
            neckCm: request.NeckCm,
            armLengthCm: request.ArmLengthCm,
            shoeSizeEu: request.ShoeSizeEu,
            bodyShape: request.BodyShape);

        _context.Avatars.Add(avatar);

        // Record initial history snapshot in same transaction
        var measurementsJson = Domain.Entities.Customer.Avatar.BuildMeasurementJson(new BodyMeasurements(
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
        ));

        var history = AvatarMeasurementHistory.CreateSnapshot(
            avatarId: avatar.Id,
            measurementDataJson: measurementsJson,
            source: request.Source);

        _context.AvatarMeasurementHistory.Add(history);

        await _context.SaveChangesAsync(cancellationToken);

        return avatar.Id;
    }
}
