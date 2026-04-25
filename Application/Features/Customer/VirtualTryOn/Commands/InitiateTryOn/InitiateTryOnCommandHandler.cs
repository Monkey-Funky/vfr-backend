using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Domain.Entities.Customer;
using Domain.Enums.Customer;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;

public sealed class InitiateTryOnCommandHandler : IRequestHandler<InitiateTryOnCommand, TryOnResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVirtualTryOnService _virtualTryOnService;

    public InitiateTryOnCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVirtualTryOnService virtualTryOnService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _virtualTryOnService = virtualTryOnService;
    }

    public async Task<TryOnResultDto> Handle(InitiateTryOnCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Validate product exists and is strictly active
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
            
        if (product is null || product.Status != "Active")
            throw new NotFoundException("Product", request.ProductId);

        // 2. Load avatar if provided, validate ownership
        Domain.Entities.Customer.Avatar? activeAvatar = null;
        if (request.AvatarId.HasValue)
        {
            activeAvatar = await _context.Avatars
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AvatarId.Value, cancellationToken);

            if (activeAvatar is null || activeAvatar.CustomerId != customerId)
                throw new NotFoundException("Avatar", request.AvatarId.Value);
        }

        // 3. Persist the pending TryOn session
        var session = VirtualTryOnSession.Create(
            customerId: customerId,
            productId: request.ProductId,
            retailerId: product.RetailerId,
            sessionType: request.SessionType,
            avatarId: request.AvatarId
        );

        _context.VirtualTryOnSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // 4. Call TryOn service
        TryOnResultDto result;
        try
        {
            result = await _virtualTryOnService.ProcessTryOnAsync(
                customerId, 
                request.ProductId, 
                request.SessionType, 
                activeAvatar, 
                cancellationToken);
        }
        catch
        {
            session.MarkAsFailed();
            await _context.SaveChangesAsync(cancellationToken);
            throw;
        }

        // 5. Update session status securely based on service result
        if (result.Status == SessionStatus.Failed)
        {
            session.MarkAsFailed();
        }
        else if (result.Status == SessionStatus.Completed && result.ResultImageUrl != null)
        {
            session.MarkAsCompleted(
                result.ResultImageUrl, 
                result.RecommendedSize, 
                result.ConfidenceScore, 
                result.DurationSeconds ?? 0);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }
}
