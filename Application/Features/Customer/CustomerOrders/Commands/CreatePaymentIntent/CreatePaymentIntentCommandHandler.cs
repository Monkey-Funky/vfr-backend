using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Domain.Entities.CustomerOrders;
using MediatR;

namespace Application.Features.Customer.CustomerOrders.Commands.CreatePaymentIntent;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, string>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStripeService _stripeService;

    public CreatePaymentIntentCommandHandler(IUnitOfWork unitOfWork, IStripeService stripeService)
    {
        _unitOfWork = unitOfWork;
        _stripeService = stripeService;
    }

    public async Task<string> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {

        var order = await _unitOfWork.Repository<COrder>().GetByIdAsync(request.COrderId, cancellationToken);
        if (order == null)
            throw new Exception("Order not found.");

        var (clientSecret, paymentIntentId) = await _stripeService.CreatePaymentIntentAsync(order.GrandTotal);

        var payment = new Payment
        {
            COrderId = order.Id,
            Amount = order.GrandTotal,
            PaymentMethod = "Credit Card",
            StripePaymentIntentId = paymentIntentId,
            TransactionDate = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Payment>().AddAsync(payment);
        
        order.CurrentStatus = "PendingPayment";

        await _unitOfWork.Repository<COrder>().UpdateAsync(order, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return clientSecret;
    }
}