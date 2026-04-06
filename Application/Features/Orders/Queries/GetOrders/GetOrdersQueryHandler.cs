using Application.Features.Orders.DTOs;
using Application.Features.Orders.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Orders.Queries.GetOrders;


/// <summary>
/// Handles GetOrdersQuery — returns paginated, filtered orders for the authenticated retailer.
///
/// FIXES APPLIED:
///   • PagedResult uses object-initializer syntax (not constructor parameters) — fixes CS1739.
///   • AsSplitQuery() removed — not available in the Application layer package.
///   • Queries always scope to RetailerId from JWT (IDOR protection).
/// </summary>
public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetOrdersQueryHandler(
        IOrderRepository orderRepository,
        ICurrentUserService currentUserService)
    {
        _orderRepository = orderRepository;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<OrderDto>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var (orders, totalCount) = await _orderRepository.GetPagedOrdersAsync(
            retailerId: retailerId,
            statusFilter: query.Status,
            searchTerm: query.SearchTerm,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        var dtos = orders.Select(o => o.ToDto()).ToList();

        // ✅ Object-initializer syntax — matches PagedResult<T>'s init properties.
        // Never use new PagedResult<T>(Items: ...) — PagedResult has no such constructor.
        return new PagedResult<OrderDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
        };
    }
}