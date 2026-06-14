using Application.Features.Customer.CustomerOrders.Commands.CreateCOrder;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Customer.CustomerOrders.Commands.CreatePaymentIntent;
using Application.Features.Customer.CustomerOrders.Commands.ConfirmPayment;
using Application.Features.Customer.CustomerOrders.Queries.GetCustomerOrders;

namespace API.Controllers;

[Route("api/customers/{customerAccountId}/orders")]
[ApiController]
//[AllowAnonymous] //for testing through API
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
    [HttpPost("{orderId}/payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent(Guid customerAccountId, Guid orderId)
    {
        var command = new CreatePaymentIntentCommand 
        { 
            COrderId = orderId 
        };
        
        var clientSecret = await _mediator.Send(command);
        
        return Ok(new 
        { 
            success = true, 
            message = "Payment Intent created successfully",
            clientSecret = clientSecret 
        });
    }

    [HttpPost("{orderId}/confirm")]
    public async Task<IActionResult> ConfirmPayment(Guid customerAccountId, Guid orderId)
    {
        var command = new ConfirmPaymentCommand 
        { 
            COrderId = orderId 
        };
        
        var result = await _mediator.Send(command);
        
        return Ok(new 
        { 
            success = true, 
            message = "Payment confirmed and order is now ready for shipping!" 
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomerOrders(Guid customerAccountId, [FromQuery] string? status = "All")
    {
        var query = new GetCustomerOrdersQuery 
        { 
            CustomerAccountId = customerAccountId,
            StatusFilter = status
        };
        
        var orders = await _mediator.Send(query);
        
        return Ok(new 
        { 
            success = true, 
            data = orders 
        });
    }
}