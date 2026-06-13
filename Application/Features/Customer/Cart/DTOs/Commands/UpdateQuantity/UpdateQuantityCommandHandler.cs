using Application.Interfaces.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Cart.Commands.UpdateQuantity;

public class UpdateQuantityCommandHandler : IRequestHandler<UpdateQuantityCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateQuantityCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateQuantityCommand request, CancellationToken cancellationToken)
    {
        if (request.NewQuantity < 0)
            return false;

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == request.CustomerAccountId, cancellationToken);

        if (cart == null) return false;

        var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (cartItem == null) return false;

        if (request.NewQuantity == 0)
        {
            cart.Items.Remove(cartItem);
        }
        else
        {
            cartItem.Quantity = request.NewQuantity;
        }

        cart.CalculateTotals();

        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}