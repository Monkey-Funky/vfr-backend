using API.Controllers.BaseControllers;
using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Profile.Commands.ChangePassword;
using Application.Features.Customer.Profile.Commands.DeleteAccount;
using Application.Features.Customer.Profile.Commands.UpdateProfile;
using Application.Features.Customer.Profile.Queries.GetProfile;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[SwaggerTag("Customer Profile — manage personal information, avatar, and account settings.")]
[Route("api/customer/profile")]
public sealed class CustomerProfileController : CustomerBaseApiController
{
    // =========================================================================
    // GET api/customer/profile
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        "Get Customer Profile",
        "Retrieves the currently authenticated customer's profile information.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetCustomerProfileQuery(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // PUT api/customer/profile
    // =========================================================================

    [HttpPut]
    [SwaggerOperation(
        "Update Customer Profile",
        "Updates primitive profile fields such as name, phone, DOB, and gender. Does not update email or password.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCustomerProfileCommand command, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }


    // =========================================================================
    // POST api/customer/profile/change-password
    // =========================================================================

    [HttpPost("change-password")]
    [SwaggerOperation(
        "Change Password",
        "Changes the customer's password and revokes all active sessions requiring them to log in again with the new password.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeCustomerPasswordCommand command, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/profile/delete-account
    // =========================================================================

    [HttpPost("delete-account")]
    [SwaggerOperation(
        "Delete Account",
        "Marks the customer account for deletion and revokes all sessions.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteCustomerAccountCommand(), cancellationToken);
        return OkResponse(result);
    }
}
