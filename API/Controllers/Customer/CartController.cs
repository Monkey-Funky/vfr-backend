using API.Controllers.BaseControllers;
using Application.Features.Customer.Cart.DTOs;
using Application.Features.Customer.Cart.Queries.GetCart;
using Application.Features.Customer.Cart.Commands.AddItemToCart;
using Application.Features.Customer.Cart.Commands.RemoveItemFromCart;
using Swashbuckle.AspNetCore.Annotations;
using Application.Features.Customer.Cart.Commands.UpdateQuantity;
using Application.Features.Customer.Cart.Commands.RemoveCart;

namespace API.Controllers.Customer;

[SwaggerTag("Customer Cart to manage shopping cart and items.")]
[Route("api/customers/{customerAccountId:guid}/cart")]
//[AllowAnonymous] for testing through API
public sealed class CartController : CustomerBaseApiController
{
    // =========================================================================
    // GET api/customers/{customerAccountId}/cart
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        "Get Customer Cart",
        "Retrieves the active shopping cart for a specific customer.")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCart(
        [FromRoute] Guid customerAccountId, 
        CancellationToken cancellationToken)
    {
        var query = new GetCartQuery { CustomerAccountId = customerAccountId };
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customers/{customerAccountId}/cart/items
    // =========================================================================

    [HttpPost("items")]
    [SwaggerOperation(
        "Add Item to Cart",
        "Adds a product to the cart. If cart doesn't exist, it is created automatically.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddItemToCart(
        [FromRoute] Guid customerAccountId,
        [FromBody] AddItemToCartCommand command,
        CancellationToken cancellationToken)
    {
        command.CustomerAccountId = customerAccountId;
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE api/customers/{customerAccountId}/cart/items/{productId}
    // =========================================================================

    [HttpDelete("items/{productId:guid}")]
    [SwaggerOperation(
        "Remove Item from Cart",
        "Removes a specific product from the customer's cart.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItemFromCart(
        [FromRoute] Guid customerAccountId,
        [FromRoute] Guid productId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveItemFromCartCommand
        { 
            CustomerAccountId = customerAccountId,
            ProductId = productId 
        };
        
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // Update api/customers/{customerAccountId}/cart/items/{productId}
    // =========================================================================

    [HttpPatch("items/{productId:guid}")]
    [SwaggerOperation(
        "Update Item Quantity",
        "Updates the quantity of an item in the cart. If the new quantity is 0, the item is removed automatically.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateQuantity(
        [FromRoute] Guid customerAccountId,
        [FromRoute] Guid productId,
        [FromBody] int newQuantity,
        CancellationToken cancellationToken)
    {
        var command = new UpdateQuantityCommand 
        { 
            CustomerAccountId = customerAccountId, 
            ProductId = productId, 
            NewQuantity = newQuantity 
        };
        
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE api/customers/{customerAccountId}/cart
    // =========================================================================

    [HttpDelete]
    [SwaggerOperation(
        "Remove Cart",
        "Deletes the entire shopping cart and all its items for the specific customer.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveCart(
        [FromRoute] Guid customerAccountId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveCartCommand 
        { 
            CustomerAccountId = customerAccountId 
        };
        
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }
}