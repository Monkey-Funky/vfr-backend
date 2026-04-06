using Application.Features.Orders.DTOs;
using Application.Features.Orders.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Handles GetOrderByIdQuery.
///
/// FIXES APPLIED:
///   • AsSplitQuery() removed — not available in Application layer.
///   • Scopes the query to retailerId AND orderId — prevents IDOR.
///   • Returns 404 (not 403) when orderId doesn't belong to the retailer —
///     avoids confirming another tenant's resource existence.
/// </summary>
public sealed class GetOrderByIdQueryHandler
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        ICurrentUserService currentUserService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
    }

    public async Task<OrderDto> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // GetByIdWithItemsAsync already filters by BOTH orderId AND retailerId.
        // If the order exists but belongs to a different retailer, null is returned → 404.
        // This is intentional IDOR defence: never return 403 (would confirm resource existence).
        var order = await _orderRepository.GetByIdWithItemsAsync(
            query.OrderId,
            retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Order), query.OrderId);

        return order.ToDto();
    }
}