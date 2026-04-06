namespace API.Controllers.Offers.Requests;

/// <summary>
/// HTTP request model for POST /api/retailers/{retailerId}/offers.
/// Binds multipart/form-data (image upload + JSON fields).
/// </summary>
public sealed record CreateOfferRequest(
    string Title,
    string? Description,
    string OfferType,       // "Product" | "Category"
    Guid? ProductId,
    Guid? CategoryId,
    string DiscountType,    // "Percentage" | "Fixed"
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly? EndDate,
    IFormFile CoverImage
);