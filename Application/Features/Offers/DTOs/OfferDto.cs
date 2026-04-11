namespace Application.Features.Offers.DTOs;

/// <summary>
/// Read model for a single Offer — returned by GetOffersQuery and GetOfferByIdQuery.
/// </summary>
public sealed record OfferDto(
    Guid Id,
    string Title,
    string? Description,
    string OfferType,
    Guid? ProductId,
    string? ProductName,     
    Guid? CategoryId,
    string? CategoryName,     
    string DiscountType,
    decimal DiscountValue,
    DateOnly StartDate,
    DateOnly? EndDate,
    string CoverImageUrl,
    string Status,
    bool IsExpired,
    bool IsActiveNow,     
    DateTime CreatedAt,
    DateTime? UpdatedAt
);