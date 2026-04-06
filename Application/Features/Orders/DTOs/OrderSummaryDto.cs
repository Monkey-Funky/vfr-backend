
namespace Application.Features.Orders.DTOs;

/// <summary>Lightweight order summary for paginated list responses.</summary>
public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string OrderDate,
    decimal TotalAmount,
    string Currency,
    string Status,
    int ItemCount
);