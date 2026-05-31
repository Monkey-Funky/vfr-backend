using Application.Features.Orders.DTOs;
using Application.Features.Orders.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Returns paginated, filtered orders for the authenticated retailer.
/// Cache-aside: TTL 2 minutes (orders change frequently on status updates).
/// Invalidated by any command that mutates an order (UpdateOrderStatus, etc.).
/// </summary>
public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetOrdersQueryHandler(
        IOrderRepository orderRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<OrderDto>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"orders:{retailerId:N}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":status{query.Status ?? "null"}" +
            $":search{query.SearchTerm ?? "null"}";

        var cached = await _cacheService.GetAsync<PagedResult<OrderDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var (orders, totalCount) = await _orderRepository.GetPagedOrdersAsync(
            retailerId: retailerId,
            statusFilter: query.Status,
            searchTerm: query.SearchTerm,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        var result = new PagedResult<OrderDto>
        {
            Items = orders.Select(o => o.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}
