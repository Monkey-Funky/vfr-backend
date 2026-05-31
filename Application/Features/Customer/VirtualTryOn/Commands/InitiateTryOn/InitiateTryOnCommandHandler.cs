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
    private readonly ICacheService _cacheService;

    public InitiateTryOnCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVirtualTryOnService virtualTryOnService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _virtualTryOnService = virtualTryOnService;
        _cacheService = cacheService;
    }

    public async Task<TryOnResultDto> Handle(InitiateTryOnCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Validate product exists and is active.
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null || product.Status != "Active")
            throw new NotFoundException("Product", request.ProductId);

        // 2. Load and validate avatar ownership if one is provided.
        Domain.Entities.Customer.Avatar? activeAvatar = null;
        if (request.AvatarId.HasValue)
        {
            activeAvatar = await _context.Avatars
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AvatarId.Value, cancellationToken);

            if (activeAvatar is null || activeAvatar.CustomerId != customerId)
                throw new NotFoundException("Avatar", request.AvatarId.Value);
        }

        // 3. Persist the pending session before calling the external service.
        var session = VirtualTryOnSession.Create(
            customerId: customerId,
            productId: request.ProductId,
            retailerId: product.RetailerId,
            sessionType: request.SessionType,
            avatarId: request.AvatarId);

        _context.VirtualTryOnSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        // 4. Call the external try-on service; mark the session failed on any exception.
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

        // 5. Update session status based on service result.
        if (result.Status == SessionStatus.Failed)
        {
            session.MarkAsFailed();
        }
        else if (result.Status == SessionStatus.Completed && result.ResultImageUrl is not null)
        {
            session.MarkAsCompleted(
                result.ResultImageUrl,
                result.RecommendedSize,
                result.ConfidenceScore,
                result.DurationSeconds ?? 0);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate the paginated try-on session list for this customer so the new
        // session appears immediately on the next fetch (all pages, all products).
        await _cacheService.RemoveByPrefixAsync($"tryon:{customerId:N}:", cancellationToken);

        return result;
    }
}
