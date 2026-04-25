using Application.Features.Customer.FitFeedback.DTOs;
using Application.Features.Customer.FitFeedback.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByOrder;

public sealed class GetFitFeedbackByOrderQueryHandler : IRequestHandler<GetFitFeedbackByOrderQuery, PagedResult<FitFeedbackDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetFitFeedbackByOrderQueryHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<FitFeedbackDto>> Handle(GetFitFeedbackByOrderQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        // First verify the order belongs to the customer
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null || order.CustomerId != customerId)
            throw new NotFoundException("Order", request.OrderId);

        // Fetch feedback joined by order item
        var query = _context.FitFeedback
            .Join(_context.OrderItems, 
                  f => f.OrderItemId, 
                  oi => oi.Id, 
                  (f, oi) => new { Feedback = f, OrderItem = oi })
            .Where(x => x.OrderItem.OrderId == request.OrderId)
            .OrderByDescending(x => x.Feedback.CreatedAt)
            .Select(x => x.Feedback)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var feedbacks = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = feedbacks.Select(f => f.ToDto()).ToList();

        return new PagedResult<FitFeedbackDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
