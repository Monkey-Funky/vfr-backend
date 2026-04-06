using Domain.Enums.Orders;


namespace Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandValidator
    : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(c => c.OrderId)
            .NotEmpty().WithMessage("OrderId is required.");

        RuleFor(c => c.RetailerId)
            .NotEmpty().WithMessage("RetailerId is required.");

        RuleFor(c => c.NewStatus)
            .NotEmpty().WithMessage("NewStatus is required.")
            .Must(OrderStatus.IsValid)
            .WithMessage($"NewStatus must be one of: {string.Join(", ", OrderStatus.All)}.");
    }
}