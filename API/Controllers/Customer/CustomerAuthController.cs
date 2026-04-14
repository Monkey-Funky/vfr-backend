using Application.Features.Customer.Auth.Commands.CompleteProfile;
using Application.Features.Customer.Auth.Commands.ForgotPassword;
using Application.Features.Customer.Auth.Commands.Login;
using Application.Features.Customer.Auth.Commands.LoginWithGoogle;
using Application.Features.Customer.Auth.Commands.Logout;
using Application.Features.Customer.Auth.Commands.RefreshToken;
using Application.Features.Customer.Auth.Commands.Register;
using Application.Features.Customer.Auth.Commands.ResetPassword;
using Application.Features.Customer.Auth.DTOs;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

/// <summary>
/// Handles all authentication flows for the VFR Customer module:
/// email/password login, Google OAuth, two-step registration,
/// JWT refresh rotation, logout, and forgot/reset password.
///
/// Inherits PublicController ([AllowAnonymous]) — never BaseApiController.
/// No JWT is required for these flows as they use alternative verification 
/// mechanisms (email OTP or TempStepToken).
/// </summary>
[SwaggerTag("Customer Authentication — account lifecycle and identity management.")]
[Route("api/customer/auth")]
public sealed class CustomerAuthController : PublicController
{
    // =========================================================================
    // POST api/customer/auth/register
    // =========================================================================

    [HttpPost("register")]
    [EnableRateLimiting("customer-auth")]
    [SwaggerOperation(
        "Registration — Step 1 (FullName + email + password)",
        "Creates a partial customer account and sends a verification email. " +
        "Always returns 200 OK if the data is valid to prevent email enumeration.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/complete-profile
    // =========================================================================

    [HttpPost("complete-profile")]
    [SwaggerOperation(
        "Registration — Step 2 (profile details)",
        "Completes customer registration. Requires the 'TempStepToken' returned from email verification. " +
        "Returns a full AuthTokenResponse on success.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteProfile(
        [FromBody] CompleteCustomerProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/login
    // =========================================================================

    [HttpPost("login")]
    [EnableRateLimiting("customer-auth")]
    [SwaggerOperation(
        "Email / password login",
        "Validates customer credentials and returns RS256 access token + refresh token. " +
        "Supports lockout after 10 failed attempts.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/login/google
    // =========================================================================

    [HttpPost("login/google")]
    [SwaggerOperation(
        "Google OAuth login",
        "Validates a Google ID token. Creates a new customer account on first sign-in. " +
        "Returns the same AuthTokenResponse envelope as email login.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LoginWithGoogle(
        [FromBody] LoginWithGoogleCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/refresh
    // =========================================================================

    [HttpPost("refresh")]
    [SwaggerOperation(
        "Refresh customer access token",
        "Accepts expired access token and valid refresh token. Uses token rotation. " +
        "Old refresh token is invalidated immediately.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshCustomerTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/logout
    // =========================================================================

    [HttpPost("logout")]
    [SwaggerOperation(
        "Logout",
        "Invalidates the customer's current refresh token session.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new LogoutCustomerCommand(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/forgot-password
    // =========================================================================

    [HttpPost("forgot-password")]
    [EnableRateLimiting("customer-auth")]
    [SwaggerOperation(
        "Forgot password — request OTP",
        "Sends a 6-digit OTP to the customer's email. Valid for 10 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/auth/reset-password
    // =========================================================================

    [HttpPost("reset-password")]
    [SwaggerOperation(
        "Reset password using OTP",
        "Verifies the email OTP and updates the password. Revokes all active sessions.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }
}
