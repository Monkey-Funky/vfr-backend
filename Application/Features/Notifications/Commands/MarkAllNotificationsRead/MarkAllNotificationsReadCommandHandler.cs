using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.MarkAllNotificationsRead;
/// <summary>
/// Marks all unread notifications for the authenticated retailer as read.
/// Uses ExecuteUpdateAsync (EF Core 7+ bulk UPDATE) — never loads records into memory.
/// Returns the count of rows updated.
/// </summary>
public sealed class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public MarkAllNotificationsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<int>> Handle(
        MarkAllNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        DateTime readAt = DateTime.UtcNow;

        // Bulk UPDATE — no in-memory loop, no individual SaveChanges per record
        int updatedCount = await _context.Notifications
            .Where(n => n.RetailerId == retailerId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, readAt),
                cancellationToken);

        await _cacheService.RemoveByPrefixAsync(
            $"notifications:{retailerId}:", cancellationToken);

        return Result<int>.Success(updatedCount,
            $"{updatedCount} notification(s) marked as read.");
    }
}