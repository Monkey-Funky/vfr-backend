namespace Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Initiates the password reset flow by generating a 6-digit OTP and sending it
/// to the retailer's email address.
///
/// SECURITY: This command always returns HTTP 200 regardless of whether the email
/// exists in the system. This prevents email enumeration attacks.
/// </summary>
public sealed record ForgotPasswordCommand(
    string Email
) : IRequest<Result<bool>>;
