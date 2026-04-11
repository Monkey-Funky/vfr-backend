using Application.Features.Auth.Commands.ForgotPassword;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.LoginWithGoogle;
using Application.Features.Auth.Commands.Logout;
using Application.Features.Auth.Commands.RefreshToken;
using Application.Features.Auth.Commands.RegisterStep1;
using Application.Features.Auth.Commands.RegisterStep2;
using Application.Features.Auth.Commands.ResetPassword;
using Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Auth;

/// <summary>
/// Handles all authentication flows for the VFR Retailer module:
/// email/password login, Google OAuth, two-step registration,
/// JWT refresh rotation, logout, and forgot/reset password.
///
/// Inherits PublicController ([AllowAnonymous]) — never BaseApiController.
/// No JWT is required to reach any action in this controller.
/// </summary>
[SwaggerTag("Authentication — login, registration, token refresh, and password reset.")]
[Route("api/auth")]
public sealed class AuthController : PublicController
{
    // =========================================================================
    // POST api/auth/login
    // =========================================================================

    [HttpPost("login")]
    [EnableRateLimiting("auth-strict")]
    [SwaggerOperation(
        "Email / password login",
        "Validates retailer credentials and returns RS256 access token + refresh token. " +
        "Access token expires in 15 minutes. " +
        "Refresh token is valid for 7 days (30 days when RememberMe = true). " +
        "Returns HTTP 401 for invalid credentials, HTTP 422 for business rule violations " +
        "(email not verified, account suspended).")]
    [ProducesResponseType(typeof(ApiResponse<Result<AuthTokenResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/auth/login/google
    // =========================================================================

    [HttpPost("login/google")]
    [EnableRateLimiting("auth-strict")]
    [SwaggerOperation(
        "Google OAuth login",
        "Validates a Google ID token server-side. Creates a new retailer account on first sign-in. " +
        "Returns the same AuthTokenResponse envelope as email login. " +
        "Returns HTTP 422 if the account exists but is in an inactive status.")]
    [ProducesResponseType(typeof(ApiResponse<Result<AuthTokenResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> LoginWithGoogle(
        [FromBody] LoginWithGoogleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/auth/register/step1
    // =========================================================================

    [HttpPost("register/step1")]
    [EnableRateLimiting("auth-strict")]
    [SwaggerOperation(
        "Registration — Step 1 (email + password)",
        "Creates a partial account (PendingEmailVerification status) and " +
        "returns a 15-minute step token (HS256 JWT). " +
        "Pass the returned step token as 'TempStepToken' form field in POST /register/step2. " +
        "Returns HTTP 409 if the email or brand name is already registered.")]
    [ProducesResponseType(typeof(ApiResponse<Result<string>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RegisterStep1(
        [FromBody] RegisterStep1Command command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<Result<string>>.SuccessResponse(
                result,
                "Registration Step 1 complete. Proceed to Step 2 to activate your account."));
    }

    // =========================================================================
    // POST api/auth/register/step2
    // =========================================================================

    [HttpPost("register/step2")]
    [EnableRateLimiting("auth-strict")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        "Registration — Step 2 (business details + optional logo)",
        "Completes retailer registration. Send the Step 1 step token as the 'TempStepToken' " +
        "form field (NOT as an Authorization header). Accepts multipart/form-data. " +
        "BrandLogoFile is optional (JPEG or PNG, max 2 MB). " +
        "Returns a full AuthTokenResponse on success with HTTP 201 Created.")]
    [ProducesResponseType(typeof(ApiResponse<Result<AuthTokenResponse>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RegisterStep2(
        [FromForm] RegisterStep2Request request,
        CancellationToken cancellationToken)
    {
        Stream? logoStream = request.BrandLogoFile?.OpenReadStream();
        string? logoFileName = request.BrandLogoFile?.FileName;
        string? logoContentType = request.BrandLogoFile?.ContentType;
        long logoSizeBytes = request.BrandLogoFile?.Length ?? 0;

        var command = new RegisterStep2Command(
            TempStepToken: request.TempStepToken,
            BusinessType: request.BusinessType,
            Has3DModels: request.Has3DModels,
            BrandLogoStream: logoStream,
            BrandLogoFileName: logoFileName,
            BrandLogoContentType: logoContentType,
            BrandLogoSizeBytes: logoSizeBytes);

        var result = await Sender.Send(command, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<Result<AuthTokenResponse>>.SuccessResponse(
                result,
                "Registration complete. Welcome to VFR!"));
    }


    // =========================================================================
    // POST api/auth/refresh-token
    // =========================================================================

    [HttpPost("refresh-token")]
    [SwaggerOperation(
        "Refresh access token",
        "Accepts the expired access token and the valid refresh token. " +
        "Returns a new RS256 access token + new refresh token pair. " +
        "The submitted refresh token is immediately invalidated (rotation pattern). " +
        "Returns HTTP 401 for all token-related failures.")]
    [ProducesResponseType(typeof(ApiResponse<Result<AuthTokenResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/auth/logout
    // =========================================================================

    [HttpPost("logout")]
    [SwaggerOperation(
        "Logout",
        "Revokes the current refresh token. Send the valid access token as " +
        "'Authorization: Bearer {token}'. " +
        "The access token remains technically valid until its 15-minute TTL expires. " +
        "Returns HTTP 204 No Content on success (including idempotent calls with no token).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // RetailerId is resolved from the JWT via ICurrentUserService inside the handler.
        await Sender.Send(new LogoutCommand(), cancellationToken);

        return NoContent();
    }

    // =========================================================================
    // POST api/auth/forgot-password
    // =========================================================================

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-relaxed")]
    [SwaggerOperation(
        "Forgot password — request OTP",
        "Sends a 6-digit OTP to the registered email address. Valid for 15 minutes. " +
        "ALWAYS returns HTTP 200 OK with the same response body regardless of whether " +
        "the email exists — this prevents email enumeration attacks.")]
    [ProducesResponseType(typeof(ApiResponse<Result<bool>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/auth/reset-password
    // =========================================================================

    [HttpPost("reset-password")]
    [SwaggerOperation(
        "Reset password using OTP",
        "Verifies the 6-digit OTP and sets a new password. " +
        "All existing sessions are revoked — the retailer must log in again. " +
        "The OTP is single-use and is deleted from Redis after this call. " +
        "Returns HTTP 422 with code 'INVALID_OTP' if the code is wrong or expired.")]
    [ProducesResponseType(typeof(ApiResponse<Result<bool>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }
}

/// <summary>
/// Multipart form model for the RegisterStep2 endpoint.
/// Exists ONLY in the API layer to decouple IFormFile (ASP.NET concern)
/// from the Application command (which uses Stream for testability).
/// </summary>
public sealed class RegisterStep2Request
{
    /// <summary>
    /// The step token returned by RegisterStep1 (send as a form field).
    /// NOT sent as an Authorization header — this is a registration continuation token.
    /// </summary>
    public string TempStepToken { get; set; } = string.Empty;

    /// <summary>Business category (e.g. "Fashion", "Electronics"). Max 50 chars.</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>Whether the retailer already has 3D product models.</summary>
    public bool Has3DModels { get; set; }

    /// <summary>Optional brand logo image (JPEG or PNG, max 2 MB).</summary>
    public IFormFile? BrandLogoFile { get; set; }
}