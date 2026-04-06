
namespace Application.Features.Orders.Commands.UpdateOrderStatus;

/// <summary>
/// Transitions an order to a new status.
///
/// SECURITY CONTRACT:
///   RetailerId MUST be set from CurrentRetailerId (JWT) in the controller.
///   It is NEVER sourced from the request body or route parameters.
///   This prevents IDOR — a retailer cannot update another retailer's order.
/// </summary>
public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    Guid RetailerId,  // ← always from JWT, never from request body
    string NewStatus) : IRequest<Result<bool>>;