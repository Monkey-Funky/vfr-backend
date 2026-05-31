using Application.Features.Orders.DTOs;
using Application.Features.Orders.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Returns a single order with full item details.
/// Cache-aside: TTL 5 minutes.
/// Invalidated by UpdateOrderStatus and any command that touches order state.
/// </summary>
public sealed class GetOrderByIdQueryHandler
    : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<OrderDto> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = CacheKeys.OrderDetail(retailerId, query.OrderId);

        var cached = await _cacheService.GetAsync<OrderDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var order = await _orderRepository.GetByIdWithItemsAsync(
            query.OrderId,
            retailerId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Order), query.OrderId);

        var dto = order.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);

        return dto;
    }
}
