namespace API.Controllers.Offers.Requests;

/// <summary>
/// HTTP request model for PUT /api/retailers/{retailerId}/offers/{offerId}.
/// Binds multipart/form-data — CoverImage is optional (null = keep existing).
/// </summary>
public sealed record UpdateOfferRequest(
    string Title,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    IFormFile? CoverImage = null
);