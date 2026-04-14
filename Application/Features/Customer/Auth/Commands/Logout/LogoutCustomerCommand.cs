namespace Application.Features.Customer.Auth.Commands.Logout;

/// <summary>
/// Revokes the current customer's session by nullifying their refresh token.
/// </summary>
public sealed record LogoutCustomerCommand : IRequest<Result<bool>>;
