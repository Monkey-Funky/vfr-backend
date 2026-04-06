using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;


namespace Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler
    : IRequestHandler<UpdateOrderStatusCommand, Result<bool>>
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
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
                    // Load WITH tracking (GetTrackedByIdAsync) so RowVersion
                    // is included in the UPDATE WHERE clause for optimistic lock.
                    // Then verify RetailerId matches JWT — if not, 404 (not 403).
                    var order = await _unitOfWork.GetTrackedByIdAsync<Order>(request.OrderId, ct)
                        ?? throw new NotFoundException(nameof(Order), request.OrderId);

                    if (order.RetailerId != request.RetailerId)
                        throw new NotFoundException(nameof(Order), request.OrderId);

                    var previousStatus = order.Status;

                    // ── State Machine Validation ──────────────────────────────
                    // Order.UpdateStatus() enforces AllowedTransitions.
                    // Invalid transition → BusinessRuleException → HTTP 422.
                    order.UpdateStatus(request.NewStatus);

                    // ── Save Status Change (still inside transaction) ─────────
                    await _unitOfWork.SaveChangesAsync(ct);

                    // ── Domain Events (inside same transaction) ───────────────
                    // All handlers call _unitOfWork.SaveChangesAsync on the SAME
                    // DbContext scope — they participate in the same transaction.
                    // If CommissionDeductionHandler throws, the ENTIRE transaction
                    // rolls back: status change + inventory decrement + notifications.
                    var domainEvent = new OrderStatusChangedEvent(
                        OrderId: order.Id,
                        RetailerId: order.RetailerId,
                        PreviousStatus: previousStatus,
                        NewStatus: request.NewStatus,
                        Items: order.Items);

                    await _mediator.Publish(domainEvent, ct);

                }, cancellationToken);

                return Result<bool>.Success(true, "Order status updated successfully.");
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxConcurrencyRetries)
            {
                _logger.LogWarning(
                    "Concurrency conflict on Order {OrderId} (attempt {Attempt}/{Max}). Retrying...",
                    request.OrderId, attempt, MaxConcurrencyRetries);

                // Small delay before retry to reduce contention
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex,
                    "Concurrency conflict on Order {OrderId} — all {Max} retries exhausted.",
                    request.OrderId, MaxConcurrencyRetries);

                throw new BusinessRuleException(
                    "ORDER_CONCURRENT_UPDATE",
                    "The order was modified by another request at the same time. Please try again.");
            }
        }

        // Unreachable — loop always returns or throws
        return Result<bool>.Failure("Unexpected error in UpdateOrderStatusCommandHandler.");
    }
}