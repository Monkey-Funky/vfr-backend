using Application.Features.Customer.CustomerOrders.Commands.CreateCOrder;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/customers/{customerAccountId}/orders")]
[ApiController]
[AllowAnonymous] //for testing through API
public class COrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public COrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(Guid customerAccountId, [FromBody] CreateCOrderCommand command)
    {
        command.CustomerAccountId = customerAccountId; 
        
        var orderId = await _mediator.Send(command);
        
        return Ok(new 
        { 
            success = true, 
            message = "Order placed successfully!", 
            orderId = orderId 
        });
    }
}