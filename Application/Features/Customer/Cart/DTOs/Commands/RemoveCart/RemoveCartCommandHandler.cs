using Application.Interfaces.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Cart.Commands.RemoveCart;

public class RemoveCartCommandHandler : IRequestHandler<RemoveCartCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RemoveCartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RemoveCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.CustomerAccountId == request.CustomerAccountId, cancellationToken);

        if (cart == null) 
            return false;

        _context.Carts.Remove(cart);
        await _context.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}