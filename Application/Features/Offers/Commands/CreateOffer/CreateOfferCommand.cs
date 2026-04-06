using Application.Common;

namespace Application.Features.Offers.Commands.CreateOffer;

/// <summary>
/// Creates a new promotional offer for the authenticated retailer.
/// RetailerId is NEVER a parameter — always resolved from ICurrentUserService.
/// </summary>
public sealed record CreateOfferCommand(
    string Title,
    string? Description,
    string OfferType,       // "Product" | "Category"
    Guid? ProductId,       // required when OfferType = "Product"
    Guid? CategoryId,      // required when OfferType = "Category"
    string DiscountType,    // "Percentage" | "Fixed"
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly? EndDate,
    FileUploadDto CoverImage       
) : IRequest<Result<Guid>>;