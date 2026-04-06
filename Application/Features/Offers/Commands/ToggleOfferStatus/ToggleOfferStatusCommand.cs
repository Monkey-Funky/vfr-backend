namespace Application.Features.Offers.Commands.ToggleOfferStatus;

/// <summary>
/// Toggles an offer's status between Active and Inactive.
/// Throws BusinessRuleException if the offer is in Expired status.
/// </summary>
public sealed record ToggleOfferStatusCommand(
    Guid OfferId
) : IRequest<Result<bool>>;
