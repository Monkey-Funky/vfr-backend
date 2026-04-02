namespace Application.Features.Auth.Commands.RegisterStep1;

/// <summary>
/// First step of two-step retailer registration.
///
/// Creates a partial RetailerAccount (Status = PendingEmailVerification) and
/// returns a short-lived signed "step token" JWT (15-minute TTL) that the
/// client must supply to RegisterStep2Command to continue registration.
///
/// The step token contains: { step: 1, temp_account_id: Guid }
/// </summary>
public sealed record RegisterStep1Command(
    string FullName,
    string Email,
    string Password,
    string BrandName
) : IRequest<Result<string>>;
