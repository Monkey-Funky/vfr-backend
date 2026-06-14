using Application.Interfaces.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Cart.Commands.AddItemToCart;

public class AddItemToCartCommandHandler : IRequestHandler<AddItemToCartCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AddItemToCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(AddItemToCartCommand request, CancellationToken cancellationToken)
    {
        // Refuse any value <= 0
        if (request.Quantity <= 0) 
            return false;

        // get cart
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == request.CustomerAccountId, cancellationToken);

        // Lazy Initialization
        if (cart == null)
        {
            cart = new Domain.Entities.Customer.Cart
            {
                CustomerAccountId = request.CustomerAccountId
            };
            _context.Carts.Add(cart);
        }

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
        }
        else
        {
            // ensure item availability
            var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
            
            if (product == null)
                throw new Exception("Product not found"); 

            var newItem = new Domain.Entities.Customer.CartItem
            {
                ProductId = request.ProductId,
                ProductName = product.Name, 
                UnitPrice = product.Price.GetValueOrDefault(),
                Quantity = request.Quantity
            };
            
            cart.Items.Add(newItem);
        }

        // Recalculate (Total Price & Quantity)
        cart.CalculateTotals();

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}