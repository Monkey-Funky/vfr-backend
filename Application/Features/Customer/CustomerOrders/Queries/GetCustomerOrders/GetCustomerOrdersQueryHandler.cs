using Application.Interfaces.Persistence;
using Domain.Entities.CustomerOrders;
using MediatR;

namespace Application.Features.Customer.CustomerOrders.Queries.GetCustomerOrders;

public class GetCustomerOrdersQueryHandler : IRequestHandler<GetCustomerOrdersQuery, List<CustomerOrderSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetCustomerOrdersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<CustomerOrderSummaryDto>> Handle(GetCustomerOrdersQuery request, CancellationToken cancellationToken)
    {
        var allOrders = await _unitOfWork.Repository<COrder>().GetAllAsync(cancellationToken);
        var orders = allOrders.Where(o => o.CustomerAccountId == request.CustomerAccountId).ToList();

        if (!string.IsNullOrEmpty(request.StatusFilter) && request.StatusFilter.ToLower() != "all")
        {
            orders = orders.Where(o => o.CurrentStatus.Equals(request.StatusFilter, StringComparison.OrdinalIgnoreCase)).ToList(); 
        }

        var result = orders.Select(o => new CustomerOrderSummaryDto
        {
            OrderId = o.Id,
            AmountPaid = o.GrandTotal,
            Discount = 0, 
            ShippingCost = o.ShippingMethod == "Send by courier" ? 70m : 0m,
            DeliveryMethod = o.ShippingMethod,
            Status = o.CurrentStatus,
            AddressSummary = o.ShippingMethod == "Send by courier" ? "Shipping Address details..." : "Store Location",
            OrderDate = o.CreatedAt
        })
        .OrderByDescending(o => o.OrderDate)
        .ToList();

        return result;
    }
}