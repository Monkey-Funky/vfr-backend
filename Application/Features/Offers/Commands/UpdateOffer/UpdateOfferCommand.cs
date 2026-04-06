
using Application.Common;

namespace Application.Features.Offers.Commands.UpdateOffer;

/// <summary>
/// Updates an existing offer. OfferType and target (ProductId/CategoryId) are immutable
/// after creation — only metadata, discount settings, dates, status, and image may change.
/// </summary>
public sealed record UpdateOfferCommand(
    Guid OfferId,
    string Title,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,            // "Active" | "Inactive" — "Expired" may not be set manually
    FileUploadDto? CoverImage = null  
) : IRequest<Result<bool>>;