using Application.Interfaces.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Cart.Commands.RemoveItemFromCart;

public class RemoveItemFromCartCommandHandler : IRequestHandler<RemoveItemFromCartCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveItemFromCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveItemFromCartCommand request, CancellationToken cancellationToken)
    {

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerAccountId == request.CustomerAccountId, cancellationToken);

        if (cart == null) return false;

        var itemToRemove = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (itemToRemove != null)
        {
            cart.Items.Remove(itemToRemove);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }
}