namespace Application.Features.Customer.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCustomerCommand(
    string Email,
    string OtpCode,
    string NewPassword
) : IRequest<Result<bool>>;
