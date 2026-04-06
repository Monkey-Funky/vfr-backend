

namespace Application.Features.Settings.Commands.ChangePassword;

/// <summary>
/// Changes the password of the currently authenticated retailer.
/// On success: all refresh tokens are revoked, forcing re-login on every device.
/// </summary>
/// <param name="CurrentPassword">The retailer's existing password for verification.</param>
/// <param name="NewPassword">The desired new password. Must satisfy complexity rules.</param>
/// <param name="ConfirmNewPassword">Must exactly match <see cref="NewPassword"/>.</param>
public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<Result<bool>>;