using Application.Interfaces.Persistence;
using Domain.Entities.CustomerOrders;
using MediatR;

namespace Application.Features.Customer.CustomerOrders.Commands.ConfirmPayment;

public class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPaymentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Repository<COrder>().GetByIdAsync(request.COrderId, cancellationToken);
        
        if (order == null)
            throw new Exception("Order not found.");

        if (order.CurrentStatus == "Confirmed")
            return true;

        order.CurrentStatus = "Confirmed";
        await _unitOfWork.Repository<COrder>().UpdateAsync(order, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}