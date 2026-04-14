using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Auth.Commands.LoginWithGoogle;

/// <summary>
/// Authenticates a customer using a Google ID token.
/// If the account does not exist, a new one is created automatically.
/// </summary>
public sealed record LoginWithGoogleCustomerCommand(
    string GoogleIdToken
) : IRequest<Result<CustomerAuthTokenResponse>>;
