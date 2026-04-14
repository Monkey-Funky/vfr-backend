using Application.Features.Customer.Auth.DTOs;

namespace Application.Features.Customer.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges an expired customer access token and a valid refresh token 
/// for a new token pair.
/// </summary>
public sealed record RefreshCustomerTokenCommand(
    string AccessToken,
    string RefreshToken
) : IRequest<Result<CustomerAuthTokenResponse>>;
