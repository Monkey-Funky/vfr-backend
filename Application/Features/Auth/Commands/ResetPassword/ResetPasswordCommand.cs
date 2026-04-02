namespace Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Completes the password reset flow by verifying the OTP and setting a new password.
///
/// On success:
///   • The password is updated (BCrypt, work factor 12)
///   • All active refresh tokens are revoked (forces re-login on all devices)
///   • The OTP is deleted from the Redis cache
/// </summary>
public sealed record ResetPasswordCommand(
    string Email,
    string OtpCode,
    string NewPassword
) : IRequest<Result<bool>>;