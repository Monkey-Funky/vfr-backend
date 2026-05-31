using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Constants;


namespace Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler
    : IRequestHandler<UpdateOrderStatusCommand, Result<bool>>
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IMediator mediator,
        ICacheService cacheService,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _mediator = mediator;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        UpdateOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    // ── IDOR Defence ─────────────────────────────────────────
                    // Load Order WITH tracking (no AsNoTracking) so the RowVersion
                    // concurrency token is included in the UPDATE WHERE clause.
                    // Include Items so that InventoryDecrementHandler can process
                    // each line item when the status transitions to Shipped.
                    var order = await _context.Orders
                        .Include(o => o.Items)
                        .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
                        ?? throw new NotFoundException(nameof(Order), request.OrderId);

                    // Verify ownership — return 404 (not 403) to avoid confirming existence
                    if (order.RetailerId != request.RetailerId)
                        throw new NotFoundException(nameof(Order), request.OrderId);

                    var previousStatus = order.Status;

                    // ── State Machine Validation ──────────────────────────────
                    // Order.UpdateStatus() enforces AllowedTransitions.
                    // Invalid transition → BusinessRuleException → HTTP 422.
                    order.UpdateStatus(request.NewStatus);

                    // ── Save status change (still inside transaction) ──────────
                    await _unitOfWork.SaveChangesAsync(ct);

                    // ── Domain Events (inside same transaction) ────────────────
                    var domainEvent = new OrderStatusChangedEvent(
                        OrderId: order.Id,
                        RetailerId: order.RetailerId,
                        PreviousStatus: previousStatus,
                        NewStatus: request.NewStatus,
                        Items: order.Items);

                    await _mediator.Publish(domainEvent, ct);

                }, cancellationToken);

                // ── Invalidate order caches after successful commit ────────────
                await Task.WhenAll(
                    _cacheService.RemoveAsync(CacheKeys.OrderDetail(request.RetailerId, request.OrderId), cancellationToken),
                    _cacheService.RemoveByPrefixAsync(CacheKeys.OrderListPrefix(request.RetailerId), cancellationToken)
                );

                return Result<bool>.Success(true, "Order status updated successfully.");
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                _logger.LogWarning(
                    "Concurrency conflict on Order {OrderId} (attempt {Attempt}/{Max}). Retrying...",
                    request.OrderId, attempt, MaxConcurrencyRetries);

                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // FIX OR-1: throw ConflictException → HTTP 409 (not BusinessRuleException → 422)
                _logger.LogError(ex,
                    "Concurrency conflict on Order {OrderId} — all {Max} retries exhausted.",
                    request.OrderId, MaxConcurrencyRetries);

                throw new ConflictException(
                    "ORDER_CONCURRENCY_CONFLICT: The order was modified by another request simultaneously. " +
                    "Please refresh and try again.");
            }
        }

        // Unreachable — loop always returns or throws
        return Result<bool>.Failure("Unexpected error in UpdateOrderStatusCommandHandler.");
    }
}