

namespace Application.Features.Offers.Commands.DeleteOffer;
/// <summary>
/// Soft-deletes an offer. The row is flagged with is_deleted = true and
/// excluded by the EF Core global query filter from all future reads.
/// </summary>
public sealed record DeleteOfferCommand(
    Guid OfferId
) : IRequest<Result<bool>>;