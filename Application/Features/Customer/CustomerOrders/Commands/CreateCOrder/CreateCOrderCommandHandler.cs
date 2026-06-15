using Application.Interfaces.Persistence;
using Domain.Entities.CustomerOrders;
using MediatR;
using Microsoft.EntityFrameworkCore; // FirstOrDefaultAsync, include

namespace Application.Features.Customer.CustomerOrders.Commands.CreateCOrder;

public class CreateCOrderCommandHandler : IRequestHandler<CreateCOrderCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context; // for advanced search

    public CreateCOrderCommandHandler(IUnitOfWork unitOfWork, IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<Guid> Handle(CreateCOrderCommand request, CancellationToken cancellationToken)
    {

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == request.CustomerAccountId, cancellationToken);

        if (cart == null || !cart.Items.Any())
            throw new Exception("Cart is empty or not found. Cannot proceed to checkout.");

        // Calculate ShippingCost depending on user preference
        decimal shippingCost = request.DeliveryMethod == "Send by courier" ? 70m : 0m;

        // Create order
        var order = new COrder
        {
            CustomerAccountId = request.CustomerAccountId,
            OrderNumber = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(), // arbitrary
            ShippingMethod = request.DeliveryMethod,
            DeliveryDate = request.DeliveryDate,
            OrderDescription = request.OrderDescription,
            
            // Calculated before in Cart
            Subtotal = cart.TotalPrice,
            MethodCost = shippingCost,
            Discount = 0, 
            GrandTotal = cart.TotalPrice + shippingCost, 
        };

        // Move cart to checkout
        foreach (var item in cart.Items)
        {
            order.COrderItems.Add(new COrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalUnitPrice = item.UnitPrice * item.Quantity,
                Size = "M" // initial
            });
        }

        // Save order with Generic Repository
        await _unitOfWork.Repository<COrder>().AddAsync(order);

        // Delete Cart
        await _unitOfWork.Repository<Domain.Entities.Customer.Cart>().DeleteAsync(cart, cancellationToken);
        
        // Save Changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}