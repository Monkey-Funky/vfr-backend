namespace Application.Features.Customer.Auth.Commands.ForgotPassword;

/// <summary>
/// Initiates the password reset flow for a customer.
/// SECURITY: This command always returns Success to prevent email enumeration.
/// </summary>
public sealed record ForgotPasswordCustomerCommand(
    string Email
) : IRequest<Result<bool>>;
