using Application.Features.Orders.DTOs;


namespace Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Returns a paginated, filtered list of orders for the authenticated retailer.
/// RetailerId is resolved from the JWT inside the handler — NOT from the request.
/// </summary>
public sealed record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? SearchTerm = null) : IRequest<PagedResult<OrderDto>>;