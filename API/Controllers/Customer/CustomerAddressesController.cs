using API.Controllers.BaseControllers;
using Application.Features.Customer.Address.Commands.CreateAddress;
using Application.Features.Customer.Address.Commands.DeleteAddress;
using Application.Features.Customer.Address.Commands.SetDefaultAddress;
using Application.Features.Customer.Address.Commands.UpdateAddress;
using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Queries.GetAddressById;
using Application.Features.Customer.Address.Queries.GetAddresses;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[SwaggerTag("Customer Addresses — manage shipping and billing addresses.")]
[Route("api/customer/addresses")]
public sealed class CustomerAddressesController : CustomerBaseApiController
{
    // =========================================================================
    // GET api/customer/addresses
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        "List Customer Addresses",
        "Retrieves all addresses for the authenticated customer.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerAddressDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCustomerAddressesQuery(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // GET api/customer/addresses/{id}
    // =========================================================================

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        "Get Address by ID",
        "Retrieves a specific address by its unique ID. Returns 404 if the address doesn't exist or belongs to another customer.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAddressById(
        [FromRoute] Guid id, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCustomerAddressByIdQuery(id), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/addresses
    // =========================================================================

    [HttpPost]
    [SwaggerOperation(
        "Create Customer Address",
        "Creates a new address. If IsDefault is true, securely unsets any existing default address in an atomic transaction.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateCustomerAddressCommand command, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        // Map 201 response directly using base OkResponse logic
        return OkResponse(result);
    }

    // =========================================================================
    // PUT api/customer/addresses/{id}
    // =========================================================================

    [HttpPut("{id:guid}")]
    [SwaggerOperation(
        "Update Customer Address",
        "Updates an existing address. IDOR protected via the handler.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAddress(
        [FromRoute] Guid id,
        [FromBody] UpdateCustomerAddressCommand command, 
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(id);

        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // DELETE api/customer/addresses/{id}
    // =========================================================================

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(
        "Delete Customer Address",
        "Deletes an address. Fails if the address is set as default.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(
        [FromRoute] Guid id, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteCustomerAddressCommand(id), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // PATCH api/customer/addresses/{id}/default
    // =========================================================================

    [HttpPatch("{id:guid}/default")]
    [SwaggerOperation(
        "Set Default Address",
        "Sets the specified address as the default shipping address. Automatically unsets the previous default address via an atomic transaction.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultAddress(
        [FromRoute] Guid id, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new SetDefaultAddressCommand(id), cancellationToken);
        return OkResponse(result);
    }
}
