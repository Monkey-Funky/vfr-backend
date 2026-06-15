using MediatR;

namespace Application.Features.Customer.CustomerOrders.Queries.GetCustomerOrders;

public class GetCustomerOrdersQuery : IRequest<List<CustomerOrderSummaryDto>>
{
    public Guid CustomerAccountId { get; set; }
    public string? StatusFilter { get; set; } // Dropdown (All, Processed, Cancelled, ...)
}