using Application.Features.Customer.FitFeedback.DTOs;
using Application.Features.Customer.FitFeedback.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.FitFeedback.Commands.SubmitFitFeedback;

public sealed class SubmitFitFeedbackCommandHandler : IRequestHandler<SubmitFitFeedbackCommand, FitFeedbackDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitFitFeedbackCommandHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<FitFeedbackDto> Handle(SubmitFitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        // 1. Verify OrderItem exists
        var orderItem = await _context.OrderItems
            .AsNoTracking()
            .FirstOrDefaultAsync(oi => oi.Id == request.OrderItemId, cancellationToken);

        if (orderItem is null)
            throw new NotFoundException("OrderItem", request.OrderItemId);

        // Verify the Order is Delivered and belongs to the customer
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderItem.OrderId, cancellationToken);

        if (order is null || order.CustomerId != customerId)
            throw new NotFoundException("OrderItem", request.OrderItemId); // 404 to avoid ID enumeration

        if (order.Status != OrderStatus.Delivered)
            throw new BusinessRuleException("ORDER_NOT_DELIVERED", "Fit feedback can only be submitted for delivered orders.");

        if (orderItem.ProductId != request.ProductId)
            throw new BusinessRuleException("PRODUCT_MISMATCH", "The provided ProductId does not match the OrderItem.");

        // 2. Check idempotency (no duplicate feedback for same OrderItem)
        var exists = await _context.FitFeedback
            .AnyAsync(f => f.OrderItemId == request.OrderItemId, cancellationToken);

        if (exists)
            throw new ConflictException("FitFeedback", "OrderItemId", request.OrderItemId);

        // 3. Create and persist feedback
        var feedback = Domain.Entities.Customer.FitFeedback.Create(
            customerId: customerId,
            orderItemId: request.OrderItemId,
            productId: request.ProductId,
            fitRating: request.FitRating,
            predictedSize: request.PredictedSize,
            actualSizeNeeded: request.ActualSizeNeeded,
            feedbackNotes: request.FeedbackNotes,
            tryOnSessionId: request.TryOnSessionId
        );

        _context.FitFeedback.Add(feedback);
        await _context.SaveChangesAsync(cancellationToken);

        return feedback.ToDto();
    }
}
