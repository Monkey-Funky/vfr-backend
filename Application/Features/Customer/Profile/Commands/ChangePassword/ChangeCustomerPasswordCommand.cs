namespace Application.Features.Customer.Profile.Commands.ChangePassword;

public sealed record ChangeCustomerPasswordCommand(
    string CurrentPassword,
    string NewPassword
) : IRequest<Result<bool>>;
