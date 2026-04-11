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
                    // GetTrackedByIdAsync loads WITH tracking so RowVersion is
                    // included in the UPDATE WHERE clause for the optimistic lock.
                    var order = await _unitOfWork.GetTrackedByIdAsync<Order>(request.OrderId, ct)
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